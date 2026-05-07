# Sprint 3 — Item E5: EnemyPoolManager Validar Reuso de NetworkObject

> **Tempo estimado**: ~2-3 horas (investigação primeiro, implementação depois).
> **Risco**: 🟡 Médio. **Pré-requisitos**: A2 + A3 mergeados.
> **Pré-leitura**: `01_padroes.md`, `memory/bug_trap_system_multiplayer.md` (se acessível),
> `memory/bug_enemy_spawn_build.md`.

## Contexto

`HordeManager.SpawnSingleEnemy()` (em `Assets/Codigo/Managers/HordeManager.cs:344`)
delega o spawn ao `EnemyPoolManager` quando disponível. Em wave de 30+ inimigos,
cada `Instantiate(prefab)` + `netObj.Spawn(true)` envia um pacote de criação para
todos os clientes. Em rajada inicial de wave (todos os inimigos spawnam em ~3s),
isso vira ~10 spawn packets/s.

**Hipótese (a confirmar)**: o `EnemyPoolManager` pode estar:
- (a) Reusando GameObjects mas chamando `Instantiate` + `Spawn` para o NetworkObject
  dentro deles (pseudo-pool — caro);
- (b) Reusando NetworkObjects via Despawn/Spawn (correto, mas pode ter bugs com
  reparenting já documentados);
- (c) Não tendo pool real e apenas instanciando.

**Nota CRÍTICA do histórico** (`memory/bug_enemy_spawn_build.md` mencionado em
MEMORY.md):
> "EnemyPoolManager deixou de reparentear NetworkObject no caminho do cliente,
> removendo o NotServerException."

Isso sugere que houve uma tentativa de pooling com reparent, e aboliu-se reparent
em clientes. **Não tentar reintroduzir reparent client-side neste item.**

## Objetivo

1. **Investigação obrigatória**: ler `EnemyPoolManager.cs` POR COMPLETO para
   entender o estado atual exato.
2. **Decidir entre 3 caminhos** baseado no que encontrar:
   - **Caminho 1 (já reusa corretamente)**: documentar e fechar item — não há
     trabalho de código, só validação quantitativa via Profiler.
   - **Caminho 2 (cria novos NetworkObjects)**: refatorar para reusar via
     `Despawn(false)` + reposicionar + `Spawn()` no servidor.
   - **Caminho 3 (estado ambíguo)**: documentar achados e PERGUNTAR ao orquestrador
     antes de mexer.
3. Se mudar código: garantir que NÃO reintroduz reparent em path de cliente
   (regressão histórica conhecida).

## Investigação prévia (obrigatória — não pular nem resumir)

### 1. Ler arquivo completo
```
Read: Assets/Codigo/Enemy/EnemyPoolManager.cs (sem offset/limit, ler tudo)
```

Anotar:
- Estrutura do pool: `Dictionary<GameObject, Queue<GameObject>>`? `List<GameObject>` por tipo?
- Método `GetPooledEnemy(prefab, position, rotation)` — o que ele faz exatamente?
  - Se a fila tem item, retira e reposiciona? Despawna+Spawna? Apenas SetActive(true)?
  - Se a fila vazia, instancia novo?
- Método `ReturnToPool(GameObject)` — o que faz?
  - SetActive(false)? Despawn?
- Há tratamento especial para `IsServer` vs cliente?
- Há código que faz `transform.SetParent(...)` em cliente? (red flag — bug histórico)

### 2. Ler `HordeManager.SpawnSingleEnemy()` e adjacências

```
Read: Assets/Codigo/Managers/HordeManager.cs (offset 344, limit 80)
```

Confirmar:
- `EnemyPoolManager.Instance.GetPooledEnemy(...)` retorna o GameObject pronto.
- O caller (`SpawnSingleEnemy`) chama `enemyController.InitializeEnemy(target, patrol, data, level, pathIndex)`
  no GameObject retornado. Isso é OK porque `InitializeEnemy` re-configura state.
- Há fallback "rede sem pool" (linha ~377-385) que faz `Instantiate + netObj.Spawn(true)`.
  Esse fallback é OK ou pode ser removido?

### 3. Ler `EnemyController.HandleDeath()` e `ReturnToPoolAfterDelay`

```
Read: Assets/Codigo/Enemy/EnemyController.cs (offset 501, limit 60)
```

Confirmar fluxo de morte:
- `HandleDeath()` para AI, anima morte, schedula `ReturnToPoolAfterDelay(1.5f)`.
- `ReturnToPoolAfterDelay` chama `EnemyPoolManager.Instance.ReturnToPool(gameObject)`.
- O ReturnToPool deve ser chamado APENAS pelo servidor (porque NGO Despawn é server-only).
  Verificar guard.

### 4. Procurar referências a `Despawn` em arquivos relacionados

```
Grep: pattern="Despawn\\(" path="Assets/Codigo/Enemy"
Grep: pattern="Despawn\\(" path="Assets/Codigo/Multiplayer/Sync/NetworkedEnemy.cs"
```

Esperar encontrar:
- `NetworkedEnemy.DieRoutine` faz `netObj.Despawn(false)` (já visto em sessão 7 Maio).
- `EnemyPoolManager` pode ou não fazer Despawn.

### 5. Reportar achados antes de implementar

Após investigação, postar para o orquestrador:
```
Investigação E5 concluída. Estado atual do EnemyPoolManager:
- [resumo da estrutura do pool em 5-10 linhas]
- Caminho identificado: 1 (já reusa) | 2 (precisa refactor) | 3 (ambíguo)
- Plano de ação proposto: [...]
Solicito confirmação antes de implementar.
```

**Aguardar confirmação do orquestrador antes de prosseguir** se for Caminho 2 ou 3.
Caminho 1 (já reusa) — pode prosseguir direto pra validação.

## Plano de mudança — Caminho 2 (se necessário)

> **APLICAR APENAS APÓS CONFIRMAÇÃO DO ORQUESTRADOR.**
> O texto abaixo é o PLANO DE REFERÊNCIA assumindo que o pool atual instancia novos
> NetworkObjects. Se a investigação revelar outra estrutura, ADAPTAR o plano e
> reportar ao orquestrador.

### Estrutura de pool por NetworkObject (servidor)

```csharp
// Pool tipado por prefab original. Cada Queue contem NetworkObjects DESPAWNED mas alive.
private Dictionary<GameObject, Queue<NetworkObject>> _serverPool = new Dictionary<GameObject, Queue<NetworkObject>>();
```

### `GetPooledEnemy` (servidor)
```csharp
public GameObject GetPooledEnemy(GameObject enemyPrefab, Vector3 position, Quaternion rotation)
{
    // Cliente: NÃO chama (apenas servidor spawna). Manter guard se já existir.
    if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
    {
        Debug.LogWarning("[EnemyPoolManager] GetPooledEnemy chamado em cliente — ignorado.");
        return null;
    }

    if (_serverPool.TryGetValue(enemyPrefab, out var queue) && queue.Count > 0)
    {
        var pooled = queue.Dequeue();
        // Reposicionar ANTES de Spawn — Spawn replica posição inicial para clientes.
        pooled.transform.SetPositionAndRotation(position, rotation);
        // Re-spawnar: cria pacote CreateObject novo. AINDA é spawn-packet, mas evita
        // realocacao de C# heap e setup do NetworkObject (mais barato que Instantiate).
        if (!pooled.IsSpawned)
            pooled.Spawn(true);
        pooled.gameObject.SetActive(true);
        return pooled.gameObject;
    }

    // Fila vazia: instancia novo. Mesmo path antigo.
    var newGo = Instantiate(enemyPrefab, position, rotation);
    if (newGo.TryGetComponent<NetworkObject>(out var netObj))
        netObj.Spawn(true);
    return newGo;
}
```

### `ReturnToPool` (servidor)
```csharp
public void ReturnToPool(GameObject enemyGo)
{
    if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsServer)
    {
        // Cliente: apenas SetActive(false). Servidor faz Despawn que propaga.
        if (enemyGo != null) enemyGo.SetActive(false);
        return;
    }

    if (enemyGo == null) return;

    // Identificar o prefab original via campo cacheado ou via tag/component.
    // Estrategia simples: usar o enemyData ou o prefab name (substring "(Clone)").
    // Se não tiver mecanismo robusto, simplesmente Destroy o GO (fallback) e logar.
    var ec = enemyGo.GetComponent<EnemyController>();
    GameObject originalPrefab = ec?.enemyData?.enemyPrefab;
    if (originalPrefab == null)
    {
        // Sem mecanismo de identificação — destruir e deixar GC pegar.
        if (enemyGo.TryGetComponent<NetworkObject>(out var noFallback))
            noFallback.Despawn(true);
        else
            Destroy(enemyGo);
        return;
    }

    if (!_serverPool.TryGetValue(originalPrefab, out var queue))
    {
        queue = new Queue<NetworkObject>();
        _serverPool[originalPrefab] = queue;
    }

    if (enemyGo.TryGetComponent<NetworkObject>(out var netObj))
    {
        // Despawn(false) — não destrói GO. Pool retém.
        if (netObj.IsSpawned)
            netObj.Despawn(false);
        enemyGo.SetActive(false);
        // IMPORTANTE: NÃO reparentear no servidor (reparenting de NetworkObject é restrito).
        // Apenas mover para fora da hierarquia visivel é OPCIONAL (deixar onde estava está OK).
        queue.Enqueue(netObj);
    }
    else
    {
        Destroy(enemyGo);
    }
}
```

### Importantes a respeitar

1. **NÃO chamar `transform.SetParent(...)` em cliente** sob qualquer circunstância
   (regressão de `NotServerException`).
2. **NÃO chamar `transform.SetParent(...)` em servidor** com NetworkObject já spawnado
   sem usar `NetworkObject.TrySetParent` — em geral, reparenting causa dor. Pular.
3. **Despawn(false)** em vez de Despawn(true) — preserva GameObject em memória.
4. **Re-Spawn** após pegar do pool: cria novo CreateObject packet. Em NGO 1.x, NetworkObject
   não pode existir "spawned" em alguns clientes e "despawned" em outros — então re-spawn
   é a única opção limpa.

### Edge case — limpar pool entre matches

Se o jogo voltar pra menu e iniciar novo match, o pool deve ser limpo. Verificar
se `EnemyPoolManager` tem método `Clear()` ou OnDestroy. Se não, adicionar:

```csharp
private void OnDestroy()
{
    foreach (var queue in _serverPool.Values)
    {
        while (queue.Count > 0)
        {
            var netObj = queue.Dequeue();
            if (netObj != null) Destroy(netObj.gameObject);
        }
    }
    _serverPool.Clear();
}
```

## Validação

### 1. Build limpo
```powershell
dotnet build PI3D.sln
```

### 2. Validação in-game (CRÍTICA — não pular)

**Cenário**: 1 host + 2 MPPM clones, cena `CenaMapaTeste`.

1. Iniciar partida normalmente.
2. Aguardar primeira wave (10 inimigos default).
3. Matar TODOS os inimigos da wave.
4. Aguardar segunda wave começar.
5. **Validar visualmente**: novos inimigos aparecem corretamente em todos os 3 jogadores
   (host + 2 clientes), com animação, IA e sons.
6. **Validar no log**:
   - Sem `NotServerException`
   - Sem warnings de NetworkBehaviour spawn duplicado
7. **Validar fluxo de morte**: matar 5 inimigos seguidos. O 6º deve aparecer e funcionar
   normalmente (validando que o pool não está corrompido).

### 3. Métrica de spawn (Network Profiler)

- Antes do fix: medir `bytes/s outbound do host` durante spawn de wave (3s iniciais).
- Depois do fix: mesma métrica.
- **Esperado**: pequena redução (Spawn() ainda envia CreateObjectMessage, mas o
  GameObject em si é reusado, então NetworkVariables iniciais podem estar pré-populadas.
  Se a hipótese E5 estiver certa, ganho é maior; se estava reusando, ganho é zero).

### 4. Edge case obrigatório — voltar pro menu e iniciar novo match

1. Match termina (Lose ou Win).
2. Volta pra `MenuScene`.
3. Iniciar novo Multiplayer.
4. Confirmar que primeira wave do segundo match spawna inimigos novos (pool foi limpo).

## Critérios de aceitação

- [ ] Build limpo (0 erros)
- [ ] Sem `NotServerException` no log durante 2 waves consecutivas
- [ ] Sem warnings de NetworkBehaviour duplicate spawn
- [ ] Inimigos visualmente OK em todos os clientes (animação, IA, sons)
- [ ] Pool limpo entre matches (segundo match spawna corretamente)
- [ ] Métrica registrada (mesmo se ganho for zero, anotar)

## Riscos e mitigação

| Risco | Probabilidade | Mitigação |
|---|---|---|
| Reintroduzir bug de reparenting | Média (zona já teve bug) | Guards explícitos `if (!IsServer) return;` em ReturnToPool. Comentar no código. |
| NetworkVariables não resetam ao reusar (HP fica zerado) | Média | `NetworkedEnemy` tem `IsDead.Value` e `NetworkHealth.Value` — `EnemyHealthSystem.InitializeHealth` re-seta no servidor. Validar que `InitializeEnemy` ainda chama `InitializeHealth`. |
| Pool cresce sem limite (memory leak) | Baixa | Aceitar — pool de inimigos cresce até `enemiesPerHordeMax`. Se ficar problema futuro, adicionar Trim. |
| Despawn(false) não preserva NetworkObject corretamente em NGO 1.12 | Baixa | Documentado no NGO. Em caso de dúvida, fallback para Destroy + Instantiate. |

## Rollback

Se algo quebrar:
```powershell
git checkout Assets/Codigo/Enemy/EnemyPoolManager.cs
git checkout Assets/Codigo/Managers/HordeManager.cs
```

Pool antigo pode ter bugs sutis mas é o estado funcional atual — preferir reverter
e investigar mais do que avançar com reuso quebrado.

## Reportar ao orquestrador (template)

```
Item: E5
Status: completed | aborted (caminho 3 — ambíguo, plano alterado)
Caminho seguido: 1 (já reusa) | 2 (refactor implementado) | 3 (abortei após investigação)
Arquivos modificados: <lista>
Build: PASS (0 erros)
Validação in-game: PASS (2 waves consecutivas + match restart) | FAIL | NOT_RUN
Métrica medida: spawn packets em wave inicial — antes: X, depois: Y
Riscos detectados: <lista>
Próximo item liberado: true (G3) | false — bloqueado por <razão>
```
