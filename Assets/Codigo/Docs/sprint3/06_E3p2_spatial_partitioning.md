# Sprint 3 — Item E3 Parte 2: Spatial Partitioning para Targeting

> **Tempo estimado**: ~1 dia. **Risco**: 🟡 Médio (refactor amplo mas isolado).
> **Pré-requisitos**: A2 + A3 + E5 + G3 mergeados.
> **Pré-leitura**: `01_padroes.md`. Este é o item maior e o último do sprint.

## Contexto

Sprints 1+2 já reduziram o custo de targeting via `OverlapSphereNonAlloc` com
buffer estático em duas hot paths:
- `EnemyController.DecideTargetTick` (~3/s por inimigo)
- `TowerController.UpdateTarget` (2/s por torre)

Em wave de 30 inimigos + 20 torres = 130 chamadas Physics/s no servidor. Com
NonAlloc (Sprint 2), eliminamos a alocação, mas a CHAMADA em si ainda custa CPU
proporcional ao número de colliders no raio (PhysX scan).

**Objetivo deste item**: substituir `Physics.OverlapSphereXxx` por consulta a
estruturas de dados próprias (registries) mantidas pelo `HordeManager` (inimigos)
e `BuildManager` (torres). Iterar uma `List<>` filtrada por distância é dramaticamente
mais rápido que physics scan, especialmente quando há muitos colliders estáticos
de cenário no mapa.

**Ganho esperado**: ~50% de redução em CPU "Server Tick" para targeting em
wave grande. Em escala maior (50+ inimigos), pode chegar a 70-80%.

## Arquitetura proposta

### Registries adicionados

#### Em `HordeManager` (já existe e gerencia inimigos)
```csharp
private static readonly List<EnemyController> _activeEnemiesRegistry = new List<EnemyController>(64);

public static void RegisterEnemy(EnemyController enemy)
{
    if (!_activeEnemiesRegistry.Contains(enemy))
        _activeEnemiesRegistry.Add(enemy);
}

public static void UnregisterEnemy(EnemyController enemy)
{
    _activeEnemiesRegistry.Remove(enemy);
}

public static IReadOnlyList<EnemyController> GetActiveEnemies() => _activeEnemiesRegistry;
```

`EnemyController.InitializeEnemy` chama `RegisterEnemy(this)`. `HandleDeath` chama
`UnregisterEnemy(this)`. `OnDestroy`/`OnDisable` também chamam Unregister para
cobrir despawn anormal.

#### Em `BuildManager` (já existe e gerencia construções)
```csharp
private readonly List<TowerController> _activeTowersRegistry = new List<TowerController>(32);

public void RegisterTower(TowerController tower)
{
    if (!_activeTowersRegistry.Contains(tower))
        _activeTowersRegistry.Add(tower);
}

public void UnregisterTower(TowerController tower)
{
    _activeTowersRegistry.Remove(tower);
}

public IReadOnlyList<TowerController> GetActiveTowers() => _activeTowersRegistry;
```

`TowerController.OnNetworkSpawn` (ou Start, dependendo da ordem de NGO) chama
`BuildManager.Instance.RegisterTower(this)`. Em `DestroyTower` ou `OnDestroy`, chama
`UnregisterTower`.

### Targeting refatorado

#### `EnemyController.DecideTargetTick` (linhas ~254-335)

```csharp
private void DecideTargetTick()
{
    float allowedRadius = (mainPriority == AITargetPriority.Player) ? findDistance : selfDefenseRadius;

    Transform nearestEntity = null;
    float nearestDistance = float.MaxValue;

    // 1. Busca Jogadores via PlayerRegistry — INALTERADO (já é eficiente).
    if (PlayerRegistry.Instance != null && PlayerRegistry.Instance.GetPlayerCount() > 0)
    {
        // ... loop existente
    }
    else
    {
        // ... fallback singleplayer existente
    }

    // 2. Busca Torres via BuildManager registry (substitui Physics.OverlapSphereNonAlloc).
    // OPTIMIZATION (Sprint 3 / Item E3p2 - 2026-05-XX): substituido por iteracao em lista
    // mantida pelo BuildManager. Em wave de 30 inimigos x 3 ticks/s = 90 OverlapSphere/s no
    // servidor. Iteracao linear de ~20 torres eh dramaticamente mais barata.
    if (BuildManager.Instance != null)
    {
        var towers = BuildManager.Instance.GetActiveTowers();
        for (int i = 0; i < towers.Count; i++)
        {
            TowerController tower = towers[i];
            if (tower == null || tower.IsDestroyed) continue;

            // Filtro por distancia. Nao precisa do allowedRadius como filter forte —
            // a comparacao com nearestDistance ja cobre.
            float distance = GetDistanceToTarget(tower.transform);
            if (distance > allowedRadius) continue; // Fora do alcance — pular.
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestEntity = tower.transform;
            }
        }
    }

    // 3. Decisão do Alvo — INALTERADO.
    // ...
}
```

Nota importante: o código antigo também detectava `NetworkedBuilding` (não-TowerController):
```csharp
if (col.GetComponent<ExoBeasts.Multiplayer.Sync.NetworkedBuilding>() != null)
{ /* ... */ }
```
Isso era para casos onde existe um NetworkedBuilding sem TowerController (shield generators,
etc.). Investigar se há buildings desse tipo. Se sim, manter um segundo registry
`_activeBuildingsRegistry` para esses casos. Se não, simplificar para apenas torres.

#### `TowerController.UpdateTarget` (linhas ~382-420)

```csharp
void UpdateTarget()
{
    Vector3 originPoint = partToRotate != null ? partToRotate.position : transform.position;

    // OPTIMIZATION (Sprint 3 / Item E3p2): substituido OverlapSphereNonAlloc por iteracao
    // em registry mantido por HordeManager. Reduz de O(N_colliders_no_raio) com Physics
    // scan para O(N_inimigos_ativos) — mais rapido em mapas com muito cenario.
    Transform nearestEnemy = null;
    float shortestDistance = Mathf.Infinity;

    var enemies = HordeManager.GetActiveEnemies();
    for (int i = 0; i < enemies.Count; i++)
    {
        EnemyController enemyController = enemies[i];
        if (enemyController == null || enemyController.IsDead) continue;
        if (enemyController.enemyData == null) continue;

        EnemyType enemyType = enemyController.enemyData.enemyType;
        bool isTargetable = (enemyType == EnemyType.Terrestre) ||
                            (TargetsFlyingEnemies && enemyType == EnemyType.Voador);
        if (!isTargetable) continue;

        // Filtro por distancia. Skin-distance (ClosestPoint) requer collider — calcular
        // distance-to-pivot inicialmente e usar ClosestPoint apenas para o melhor candidato.
        // Otimizacao adicional: pular ClosestPoint se distance-to-pivot > range.
        float pivotDistance = Vector3.Distance(originPoint, enemyController.transform.position);
        if (pivotDistance > CurrentRange + 2f) continue; // 2f = margem para enemy radius

        // Apenas para candidatos viaveis, calcular distance-to-skin precisa.
        Collider enemyCol = enemyController.GetComponentInChildren<Collider>();
        Vector3 closestPoint = enemyCol != null ? enemyCol.ClosestPoint(originPoint) : enemyController.transform.position;
        float distanceToSkin = Vector3.Distance(originPoint, closestPoint);

        if (distanceToSkin > CurrentRange) continue;

        if (distanceToSkin < shortestDistance)
        {
            shortestDistance = distanceToSkin;
            nearestEnemy = enemyController.transform;
        }
    }

    targetEnemy = nearestEnemy;
}
```

**Importante**: a lógica de `enemyTag.CompareTag` foi substituída por checagem direta de
`enemyData.enemyType`. Validar que esse campo é setado em todos os inimigos via
`InitializeEnemy(initialTarget, points, data, level, pathIndex)`.

## Investigação prévia (obrigatória)

### 1. Confirmar o ciclo de vida atual de inimigos
```
Read: Assets/Codigo/Enemy/EnemyController.cs (offset 84, limit 50)
Read: Assets/Codigo/Enemy/EnemyController.cs (offset 501, limit 60)
```
Anotar:
- `InitializeEnemy(...)` é o ponto de entrada quando inimigo é spawnado/reusado.
- `HandleDeath()` é chamado em morte. `ReturnToPoolAfterDelay` despawna após delay.
- Se Sprint 3 / E5 mexeu no fluxo de pool, o registry deve ser atualizado consistentemente:
  - `InitializeEnemy` → `RegisterEnemy(this)` (Inimigo entra ativo)
  - `HandleDeath` → `UnregisterEnemy(this)` (sai imediatamente, antes de qualquer delay)

### 2. Confirmar ciclo de vida de torres
```
Read: Assets/Codigo/Towers/TowerController.cs (offset 67, limit 100)
Read: Assets/Codigo/Towers/TowerController.cs (offset 489, limit 50)
```
Anotar:
- `Awake` ou `Start` é onde inicializar.
- `DestroyTower` desativa renderers (não destrói o GameObject — pra suportar Revive).
  - Registry deve usar `IsDestroyed` como flag para pular, não Unregister.
- `Revive` reativa — registry continua válido.

### 3. Verificar usos atuais do código antigo

```
Grep: pattern="OverlapSphereNonAlloc|OverlapSphere\\(" path="Assets/Codigo/Enemy"
Grep: pattern="OverlapSphereNonAlloc|OverlapSphere\\(" path="Assets/Codigo/Towers"
```

Confirmar que os únicos usos em hot path são `EnemyController.DecideTargetTick` e
`TowerController.UpdateTarget`. Outros usos (Espinhos.cs, etc.) podem ficar como
estão — não fazem parte deste item.

### 4. Existe `NetworkedBuilding` sem TowerController?

```
Grep: pattern="class.*NetworkedBuilding" path="Assets/Codigo"
Grep: pattern="GetComponent<.*NetworkedBuilding>" path="Assets/Codigo"
```

Se há GameObjects que têm `NetworkedBuilding` mas NÃO têm `TowerController`
(ex: shields, generators), incluí-los no targeting de inimigos requer um segundo
registry ou um TowerController-like base type.

Se não houver, simplificar.

### 5. Reportar achados antes de implementar

```
Investigação E3p2 concluída:
- Registry para inimigos: HordeManager (pré-existe ou novo?)
- Registry para torres: BuildManager (pré-existe ou novo?)
- NetworkedBuilding sem TowerController: SIM (lista) | NÃO
- Plano confirmado | adaptado para: [...]
Solicito confirmação antes de implementar.
```

## Plano de implementação

### Ordem de aplicação dos commits (dentro deste item)

1. **Commit 1**: adicionar registries em `HordeManager` e `BuildManager`. Ainda não
   trocar o targeting — só popular os registries. Build deve passar; código antigo
   ainda funciona via Physics.

2. **Commit 2**: refatorar `EnemyController.DecideTargetTick` para usar registry
   de torres. Manter Physics como fallback se `BuildManager.Instance == null` (singleplayer
   pode não ter BuildManager spawnado). Validar build + teste in-game inimigos
   atacando torres.

3. **Commit 3**: refatorar `TowerController.UpdateTarget` para usar registry de
   inimigos. Manter Physics como fallback. Validar build + teste in-game torres
   atacando inimigos.

4. **Commit 4**: cleanup — remover código antigo de Physics se fallback não foi
   acionado durante testes.

### Pontos de atenção

**Thread-safety**: Unity Physics + scripts rodam em main thread, então `List<>` é OK
sem lock. Não usar `lock` ou Concurrent collections.

**Iteração durante modificação**: durante `DecideTargetTick`, é possível que outro código
modifique a lista? Improvável (todo gameplay é main thread), mas:
- Se durante o loop um inimigo morrer (UnregisterEnemy) — usar índice e checar null:
  ```csharp
  var towers = BuildManager.Instance.GetActiveTowers();
  for (int i = 0; i < towers.Count; i++)
  {
      TowerController tower = towers[i];
      if (tower == null) continue; // remoção do registry pode deixar buracos transitorios
      // ...
  }
  ```
- `IReadOnlyList<>` não previne modificação por trás (é apenas read-only contract). Aceito.

**Ordem de spawn**: TowerController pode ser spawnado ANTES de BuildManager existir
(em raríssimos casos de race). Em `RegisterTower` e `UnregisterTower`:
```csharp
public void RegisterTower(TowerController tower)
{
    if (tower == null) return;
    if (!_activeTowersRegistry.Contains(tower))
        _activeTowersRegistry.Add(tower);
}
```

E em `TowerController.Start`:
```csharp
void Start()
{
    if (towerData == null) { this.enabled = false; return; }
    // ... resto

    if (BuildManager.Instance != null)
        BuildManager.Instance.RegisterTower(this);
    else
        StartCoroutine(RegisterWhenBuildManagerReady()); // fallback
}

private IEnumerator RegisterWhenBuildManagerReady()
{
    while (BuildManager.Instance == null)
        yield return null;
    BuildManager.Instance.RegisterTower(this);
}
```

## Validação

### Build
- [ ] `dotnet build PI3D.sln` retorna 0 erros após cada commit.

### Validação in-game (cenário CenaMapaTeste com 1 host + 2 MPPM)

#### Após Commit 1 (registries adicionados, sem mudança de targeting)
- [ ] Sem regressão. Comportamento idêntico ao baseline.
- [ ] Logs (em UNITY_EDITOR) podem mostrar populating de registries.

#### Após Commit 2 (inimigos via registry de torres)
- [ ] Inimigos atacam torres normalmente.
- [ ] Inimigos detectam jogadores normalmente.
- [ ] Inimigos detectam torres em wave grande (não há "buraco" de detecção).

#### Após Commit 3 (torres via registry de inimigos)
- [ ] Torres atiram em inimigos em range.
- [ ] Torres mudam alvo quando inimigo atual sai do range / morre.
- [ ] Sem inimigo "imune" ao targeting de torre.

#### Após Commit 4 (cleanup)
- [ ] Tudo continua funcionando (smoke test).

### Métricas (Network Profiler + CPU Profiler)

Cenário: 30 inimigos + 8 torres + 3 jogadores em combate por 60s.

- [ ] CPU servidor "Server Tick" antes vs depois — esperar redução ≥ 30%.
- [ ] Profiler GC alloc/s — esperar manter zero (já estava após Sprint 2).

## Riscos e mitigação

| Risco | Probabilidade | Mitigação |
|---|---|---|
| Registry desatualizado (inimigo morto ainda no registry) | Média | Sempre check `IsDead` ou null no loop. Unregister no `HandleDeath`, redundante em OnDestroy. |
| Race entre register e first-targeting | Baixa | Inimigos são spawnados pelo HordeManager antes de qualquer tower targeting. Torres existem antes de waves começarem. |
| BuildManager null em singleplayer/teste isolado | Baixa | Fallback para Physics OverlapSphereNonAlloc se registry não disponível. |
| Comportamento sutilmente diferente em alvo escolhido (ClosestPoint vs pivot distance) | Média | Mantida lógica de ClosestPoint para candidatos viáveis — apenas pré-filtro com pivot distance. Mesma resolução final. |

## Rollback

Cada commit é independente. Para reverter:
```powershell
git log --oneline   # achar SHA
git revert <sha>
```

Reverter o item inteiro:
```powershell
git checkout main
```

## Reportar ao orquestrador (template — após CADA commit)

```
Item: E3p2 — Commit <1|2|3|4>
Status: completed | aborted
Arquivos modificados: <lista>
Build: PASS (0 erros)
Validação in-game: PASS | FAIL (<qual check>)
Métrica medida: <CPU Server Tick antes/depois> | NOT_RUN
Riscos detectados: <lista>
Próximo commit liberado: true | false
Item E3p2 completo: false (commits 1-3 ainda) | true (após commit 4)
```

Final report após commit 4:
```
Item: E3p2 (todos os commits)
Status: completed
Sprint 3 inteiro concluído: ✅ A2, A3, E5, G3, E3p2 todos mergeados
Métrica final: CPU Server Tick em wave 30+inimigos: <antes> → <depois> (X% redução)
Próximos passos: Sprint 4 (limpeza) — itens G6, G7, A6, E6, E7, V1
```
