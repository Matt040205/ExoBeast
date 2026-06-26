# Alertas de Migracao — ExoBeasts V3 NGO
# Problemas de baixa prioridade identificados durante auditoria

Ultima atualizacao: 2026-03-24

---

## Sprint 1-4: Alertas Pendentes

### 1. Memory Leak em PlayerHealthSystem.OnValueChanged
**Arquivo:** `Assets/Codigo/Char scripts/Player/PlayerHealthSystem.cs:44`
**Severidade:** Media
**Descricao:** O callback `currentHealth.OnValueChanged` eh registrado como lambda anonima no `OnNetworkSpawn`, mas nunca eh removido no `OnNetworkDespawn`. Se o jogador for despawnado e respawnado (reconexao), o callback antigo permanece, causando invocacoes duplicadas.
**Correcao:**
```csharp
// Trocar lambda por metodo nomeado:
private void OnHealthValueChanged(float oldVal, float newVal) => NotifyHealthChanged();

public override void OnNetworkSpawn()
{
    // ...
    currentHealth.OnValueChanged += OnHealthValueChanged;
}

public override void OnNetworkDespawn()
{
    currentHealth.OnValueChanged -= OnHealthValueChanged;
    base.OnNetworkDespawn();
}
```

### 2. GetComponent via String em RespawnClientRpc
**Arquivo:** `Assets/Codigo/Char scripts/Player/PlayerHealthSystem.cs:194`
**Severidade:** Baixa
**Descricao:** `GetComponent("PlayerMovement") as MonoBehaviour` usa busca por string. Fragil a renomeacoes e mais lento que busca por tipo.
**Correcao:**
```csharp
// Trocar por:
var movementScript = GetComponent<PlayerMovement>();
```

### 3. SphereCollider Adicionado em Runtime no EnemyCombatSystem
**Arquivo:** `Assets/Codigo/Enemy/EnemyCombatSystem.cs:60`
**Severidade:** Media
**Descricao:** `InitializeCombat` chama `AddComponent<SphereCollider>()` no servidor se nao existir. Em multiplayer, clientes nao recebem esse componente adicionado em runtime (colliders nao sao sincronizados pelo NGO). Pode causar inconsistencias.
**Correcao:** Adicionar o SphereCollider diretamente no prefab do inimigo no Editor Unity, com `isTrigger = true` e o radius desejado. Remover o `AddComponent` do codigo.

### 4. EnemyPoolManager Nao Usa INetworkPrefabInstanceHandler
**Arquivo:** `Assets/Codigo/Enemy/EnemyPoolManager.cs`
**Severidade:** Baixa (funcional mas nao otimo)
**Descricao:** O pool atual usa `Spawn(true)` / `Despawn(false)` manual. O NGO recomenda `INetworkPrefabInstanceHandler` para que o framework controle Instantiate/Destroy automaticamente. O problema atual: quando o servidor faz `Spawn()`, os clientes recebem o objeto via `Instantiate()` do NGO (nao do pool), criando copias extras no lado do cliente.
**Correcao futura:** Implementar `INetworkPrefabInstanceHandler` e registrar via `NetworkManager.Singleton.PrefabHandler.AddHandler()`. Isso garante que TODOS os lados (servidor e clientes) usam o pool.

### 5. FindFirstObjectByType em EnemyController.AttackObjectiveAndDie
**Arquivo:** `Assets/Codigo/Enemy/EnemyController.cs:222`
**Severidade:** Baixa
**Descricao:** `FindFirstObjectByType<ObjectiveHealthSystem>()` eh chamado toda vez que um inimigo chega ao fim da patrulha. Com muitos inimigos, isso gera buscas desnecessarias.
**Correcao:** Cachear a referencia no `InitializeEnemy` ou usar `ObjectiveHealthSystem.Instance` (adicionar Singleton ao ObjectiveHealthSystem).

### 6. Invoke com String em PlayerShooting.StartReloadLocal
**Arquivo:** `Assets/Codigo/Char scripts/Player/PlayerShooting.cs:313`
**Severidade:** Baixa
**Descricao:** `Invoke("FinishReload", characterData.reloadSpeed)` usa string para referenciar metodo. Fragil a renomeacoes e nao detectado pelo compilador.
**Correcao futura:** Trocar por `StartCoroutine` ou `Awaitable`:
```csharp
private IEnumerator ReloadRoutine()
{
    yield return new WaitForSeconds(characterData.reloadSpeed);
    FinishReload();
}
```

### 7. Camera.main Chamado no Update do BuildManager
**Arquivo:** `Assets/Codigo/Tower scripts/BuildManager.cs:118`
**Severidade:** Baixa
**Descricao:** `Camera.main` faz `FindGameObjectWithTag("MainCamera")` internamente. Chamado a cada frame no `HandleBuildGhost`.
**Correcao:** Cachear em campo privado no `OnNetworkSpawn` ou `Awake`.

### 8. GetComponent<MergulhoTintaLogic> no Input de Pulo
**Arquivo:** `Assets/Codigo/Char scripts/Player/PlayerMovement.cs:147`
**Severidade:** Baixa
**Descricao:** `GetComponent<MergulhoTintaLogic>()` eh chamado toda vez que o jogador tenta pular. Deveria ser cacheado no Awake.
**Correcao:** Adicionar campo privado e cachear no `Awake()`.

### 9. Cooldowns de Habilidades Nao Sincronizados
**Arquivo:** `Assets/Codigo/Char scripts/JP/CommanderAbilityController.cs`
**Severidade:** Baixa (para MVP)
**Descricao:** O dicionario `abilityCooldowns` eh local por cliente. Se um jogador desconectar e reconectar, os cooldowns sao resetados. Para o MVP eh aceitavel, mas para producao, considerar sincronizar via NetworkList ou struct serializada.

### 10. TrapLogicBase.SellTrap Sem Feedback Visual
**Arquivo:** `Assets/Codigo/Tower scripts/Armadilhas/TrapLogicBase.cs`
**Severidade:** Baixa
**Descricao:** A venda da armadilha faz `Despawn()` no servidor mas nao tem ClientRpc para efeito visual/sonoro de venda. O objeto simplesmente desaparece.
**Correcao futura:** Adicionar `SellVisualClientRpc()` antes do `Despawn()` com particula/som.

---

## Sprint 5: Alertas Pendentes

### 11. Lambda Anonima em ObjectiveHealthSystem.OnNetworkSpawn
**Arquivo:** `Assets/Codigo/Managers/ObjectiveHealthSystem.cs:33`
**Severidade:** Baixa (objetivo eh objeto de cena, nunca respawnado)
**Descricao:** `currentHealth.OnValueChanged += (oldVal, newVal) => OnHealthChanged?.Invoke()` registra lambda anonima no `OnNetworkSpawn` mas nunca remove no `OnNetworkDespawn`. Mesmo padrao do Alerta #1 (PlayerHealthSystem). Como o objetivo nao eh despawnado/respawnado, o risco eh minimo.
**Correcao:**
```csharp
private void OnCurrentHealthChanged(float oldVal, float newVal) => OnHealthChanged?.Invoke();

public override void OnNetworkSpawn()
{
    currentHealth.OnValueChanged += OnCurrentHealthChanged;
}

public override void OnNetworkDespawn()
{
    currentHealth.OnValueChanged -= OnCurrentHealthChanged;
    base.OnNetworkDespawn();
}
```

### 12. TutorialPopupUI Encoding Garbled (Corrigido)
**Arquivo:** `Assets/Codigo/Managers/Saves/Tutorial/TutorialPopupUI.cs:31`
**Severidade:** Baixa (corrigido nesta sprint)
**Descricao:** Comentario com `nÃ£o` (UTF-8 garbled) em vez de `nao`. Corrigido para ASCII seguro.

### 13. UIManager.ShowPauseMenu Tinha Debug.Log Spam (Corrigido)
**Arquivo:** `Assets/Codigo/Managers/UIManager.cs`
**Severidade:** Baixa (corrigido nesta sprint)
**Descricao:** Debug.Log no Awake e ShowPauseMenu executavam a cada chamada. Removidos.

---

## Sprint 6: Alertas Pendentes

### 14. BleedingBehavior sem implementacao de ApplyBleed
**Arquivo:** `Assets/Codigo/Char scripts/Coruja/Caminhos/Alcance/Scripts/BleedingBehavior.cs:22`
**Severidade:** Media
**Descricao:** O metodo `HandleCriticalHit` esta presente e corretamente protegido por IsServer, mas a chamada `target.ApplyBleed(bleedDamagePerSecond, bleedDuration)` esta comentada porque `EnemyHealthSystem` ainda nao tem esse metodo. O comportamento de sangramento nao e aplicado em jogo.
**Correcao:** Implementar `ApplyBleed(float dps, float duration)` no `EnemyHealthSystem` (cria coroutine de dano periodico no servidor) e descomentar a chamada em `BleedingBehavior`.

### 15. OwlEyeBehavior sem implementacao de ApplyReveal
**Arquivo:** `Assets/Codigo/Char scripts/Coruja/Caminhos/Alcance/Scripts/OwlEyeBehavior.cs:22`
**Severidade:** Baixa
**Descricao:** O comportamento de revelar inimigos (remover fog-of-war ou outline) esta sem implementacao. A estrutura de rede esta correta (IsServer guard), mas o efeito visual precisa de ClientRpc para sincronizar o outline nos clientes.
**Correcao futura:**
```csharp
// Em EnemyHealthSystem:
[ClientRpc]
public void ApplyRevealClientRpc(float duration) { /* ativa outline visual */ }

// Em OwlEyeBehavior.HandleCriticalHit:
target.ApplyRevealClientRpc(revealDuration);
```

### 16. ArmorAuraBehavior executa OverlapSphere todo frame
**Arquivo:** `Assets/Codigo/Char scripts/Raposa/caminhos/Proteçao/Scripts/ArmorAuraBehavior.cs:14`
**Severidade:** Baixa
**Descricao:** `UpdateAuraEffect()` e chamado a cada frame via `Update()`. Com multiplos jogadores cada um com uma aura, pode causar picos de CPU. `HealingAuraBehavior` ja usa tick de 1s como referencia.
**Correcao:** Adicionar timer como em `LegacyAuraBehavior` (tick de 0.5s):
```csharp
private float timer;
private void Update()
{
    if (!IsServer) return;
    timer += Time.deltaTime;
    if (timer >= 0.5f) { UpdateAuraEffect(); timer = 0f; }
}
```

### 17. VooGraciosoLogic chama DestroyLogic duas vezes no Host
**Arquivo:** `Assets/Codigo/Char scripts/Coruja/VooGraciosoLogic.cs:75`
**Severidade:** Baixa (nao causa erro — IsSpawned guard previne duplo Despawn)
**Descricao:** No Host (IsServer=true e IsOwner=true para o prefab spawnado), o bloco `if (IsOwner)` chama `RequestDestroyServerRpc()` e o bloco `if (IsServer)` chama `DestroyLogic()` diretamente no mesmo frame. O segundo Despawn e ignorado por `if (NetworkObject.IsSpawned)`, mas e redundante.
**Correcao futura:** Adicionar flag `destroyed` para evitar dupla chamada:
```csharp
private bool destroyRequested;
if (IsOwner && playerMovement.isGrounded && !destroyRequested)
{
    destroyRequested = true;
    RequestDestroyServerRpc();
}
```

### 18. NineTailsDanceLogic.originalAttackRange nao inicializado antes de RemoveUltimateEffects
**Arquivo:** `Assets/Codigo/Char scripts/Raposa/NineTailsDanceLogic.cs:95`
**Severidade:** Baixa
**Descricao:** Se um cliente entrar na sessao enquanto a ultimate esta ativa, `OnNetworkSpawn` chama `ApplyUltimateEffects()` que salva o range atual em `originalAttackRange`. Mas se o estado mudar de true para false enquanto o cliente esta conectando (raca de condicao), `RemoveUltimateEffects()` e chamado com `originalAttackRange = 0`, zerando o range do `swordStats`.
**Correcao futura:** Inicializar `originalAttackRange` a partir de `meleeSystem.swordStats.attackRange` em `OnNetworkSpawn` antes de registrar o callback.

### 19. Dragao e Polvo — scripts nao migrados para NGO (Sprint futura)
**Arquivo:** `Assets/Codigo/Char scripts/Dragao/` e `Assets/Codigo/Char scripts/Polvo/`
**Severidade:** Alta (bloqueia multiplayer para esses personagens)
**Descricao:** Todos os scripts de Dragao (AquiNaoLogic, PosturaBaluarteLogic, TemorSismicoLogic, HabilidadeAquiNao, HabilidadePosturaBaluarte, HabilidadeTemorSismico, PassiveEscamasAdamantium) e Polvo (MergulhoTintaLogic, ObraPrimaLogic, NuvemDeTintaLogic, TracoUrbanoLogic, BombaSprayProjectile, ProjetilColorido, PaintAbilitySystem, e Habilidade*.cs) ainda herdam de `MonoBehaviour`, usam `Destroy()` em vez de `Despawn()`, e nao tem guards de IsServer/IsOwner. Nenhuma logica de rede foi implementada nesses personagens.
**Correcao:** Migrar Dragao e Polvo na Sprint 6 continuacao ou Sprint 7, seguindo o mesmo padrao aplicado em Raposa e Coruja (ServerRpc para ativacao, IsServer para dano, ClientRpc para VFX/SFX, Despawn em vez de Destroy).

### 20. MultiShotBehavior.FireExtraProjectiles sem implementacao de FireProjectileAt
**Arquivo:** `Assets/Codigo/Char scripts/Raposa/caminhos/dano/Scripts/MultiShotBehavior.cs:43`
**Severidade:** Media
**Descricao:** A logica de selecao de alvos aleatorios esta implementada, mas a linha de disparo `towerController.FireProjectileAt(...)` esta comentada porque o metodo nao existe ainda no `TowerController`.
**Correcao:** Implementar `FireProjectileAt(Transform target, float damage)` no `TowerController` para disparar um projetil extra em direcao ao alvo especificado.

---

## Sprint 7: Alertas Pendentes

### 21. BotaoHabilidade.FindObjectOfType<Rastros> no Menu de Selecao
**Arquivo:** `Assets/Codigo/Char scripts/Player/BotaoHabilidade.cs:51,103`
**Severidade:** Baixa (chamada unica no menu, nao por frame)
**Descricao:** O script de UI de selecao de personagem usa `FindObjectOfType<Rastros>()` em dois metodos distintos. Como eh chamado apenas ao abrir o menu (nao no loop de jogo), o impacto de performance eh minimo. Porem, eh fragil se o objeto `Rastros` for renomeado.
**Correcao futura:** Adicionar Singleton a `Rastros` (ou injetar via Inspector) e trocar por `Rastros.Instance`.

### 22. PassiveEscamasAdamantium.FindObjectsOfType<TowerController>
**Arquivo:** `Assets/Codigo/Char scripts/Dragao/PassiveEscamasAdamantium.cs:19,40`
**Severidade:** Media (Dragao nao migrado — bloqueia multiplayer para Dragao)
**Descricao:** A passiva do Dragao usa `FindObjectsOfType<TowerController>()` para aplicar bonus de armadura a todas as torres. Em multiplayer, isso: (a) executa em todos os clientes sem guard IsServer; (b) busca objetos de cena a cada chamada em vez de usar cache. Alem disso, `PassiveEscamasAdamantium` ainda herda de `MonoBehaviour` — toda a familia Dragao precisa ser migrada para NGO.
**Correcao:** Migrar Dragao para NGO (Sprint 6 continuacao / Sprint 7): adicionar guard `if (!IsServer)`, cachear lista de TowerControllers via BuildManager ou evento de construcao.

### 23. ObjectiveHealthSystem lambda anonima em OnNetworkSpawn (Corrigido na Sprint 7)
**Arquivo:** `Assets/Codigo/Managers/ObjectiveHealthSystem.cs:33`
**Severidade:** Baixa (corrigido nesta sprint)
**Descricao:** Mesmo padrao do Alerta #1 e #11. Callback anonimo substituido por metodo nomeado `OnCurrentHealthChanged`; unsubscricao adicionada em `OnNetworkDespawn`. Singleton `static Instance` adicionado para eliminar FindObjectOfType em EnemyController e PlayerHUD.

---

## Sprint 8: Alertas Pendentes

### 24. MatchManager.EndMatchVictory/Defeat nao chamados pelo fluxo atual
**Arquivo:** `Assets/Codigo/Multiplayer/GameServer/MatchManager.cs:119,128`
**Severidade:** Baixa (funcional — fluxo de cena funciona sem o MatchManager)
**Descricao:** `EndMatchVictory()` e `EndMatchDefeat()` atualizam `CurrentMatchState` para Victory/Defeat, mas HordeManager chama diretamente `NetworkManager.Singleton.SceneManager.LoadScene("Win")` e ObjectiveHealthSystem chama LoadScene("Lose") sem passar pelo MatchManager. O estado do MatchManager nunca chega a Victory/Defeat via fluxo normal. Pode causar inconsistencia se outros sistemas consultarem `CurrentMatchState` esperando essas transicoes.
**Correcao futura:** Conectar os gatilhos: `HordeManager.OnWaveCompleted` deve chamar `MatchManager.Instance.EndMatchVictory()` antes do LoadScene; `ObjectiveHealthSystem.Die` deve chamar `MatchManager.Instance.EndMatchDefeat()`.

### 25. MatchManager usa Invoke(nameof(...)) para delays de inicio
**Arquivo:** `Assets/Codigo/Multiplayer/GameServer/MatchManager.cs:62,91`
**Severidade:** Baixa
**Descricao:** `Invoke(nameof(StartMatch), matchStartDelay)` e `Invoke(nameof(BeginPlaying), matchStartDelay)` usam `nameof()` — imune a renomeacoes — mas `Invoke` nao eh cancelavel. Se o objeto for despawnado durante o delay (ex: partida abortada), o metodo ainda sera chamado. Mesmo padrao do Alerta #6.
**Correcao futura:** Substituir por coroutines com `destroyCancellationToken`:
```csharp
private IEnumerator StartMatchWithDelay()
{
    yield return new WaitForSeconds(matchStartDelay);
    if (IsServer) StartMatch();
}
```

---

## Verificacao Cruzada (Opus sobre Sonnet): Alertas Adicionais

### 26. TowerBehavior subclasses usam OnDestroy em vez de OnNetworkDespawn (13 scripts)
**Arquivos:** Todos os scripts que herdam de `TowerBehavior` em `Coruja/Caminhos/` e `Raposa/caminhos/`
**Severidade:** Media
**Descricao:** BleedingBehavior, OwlEyeBehavior, ArrowRainBehavior, FuryStackyBehavior, ReloadSpeedBehavior, FlyingEnemyTargetingBehavior, PreyMarkBehavior, ArmorShredBehavior, BonusDamageToShreddedBehavior, DoubleAttackBehavior, AssaultBehavior, FuryStackBehavior e MultiShotBehavior usam `OnDestroy()` para remover event listeners (`towerController.OnCriticalHit -=`, etc.). Como sao `NetworkBehaviour` (via `TowerBehavior`), devem usar `OnNetworkDespawn()`. Se pooling via `INetworkPrefabInstanceHandler` for implementado no futuro, `OnDestroy` nao sera chamado no Despawn e os listeners ficarao pendurados.
**Correcao:** Trocar `OnDestroy()` por `OnNetworkDespawn()` em cada subclass:
```csharp
// Em cada TowerBehavior subclass:
public override void OnNetworkDespawn()
{
    if (towerController != null) towerController.OnCriticalHit -= HandleCriticalHit;
    base.OnNetworkDespawn();
}
```

### 27. NineTailsDanceAbility nao re-habilitava componente antes de StartEffect (Corrigido)
**Arquivo:** `Assets/Codigo/Char scripts/Raposa/NineTailsDanceAbility.cs:35`
**Severidade:** Baixa (corrigido nesta verificacao)
**Descricao:** CuttingBladeAbility (linha 52) e PeaceOfMindAbility (linha 29) fazem `logic.enabled = true` antes de chamar o metodo de efeito. NineTailsDanceAbility nao fazia, causando inconsistencia. Embora StartCoroutine funcione em MonoBehaviours com enabled=false, o padrao deve ser uniforme.

### 28. Dragao/Polvo: TakeDamage chamado com 1 parametro em scripts nao-migrados
**Arquivos:** `AquiNaoLogic.cs:17`, `TemorSismicoLogic.cs:26`, `PosturaBaluarteLogic.cs:68`, `ObraPrimaLogic.cs:59`
**Severidade:** Alta (compila gracas a parametros default, mas nao passa armorPenetration)
**Descricao:** Todos os scripts de Dragao/Polvo chamam `TakeDamage(damage)` com 1 param. `EnemyHealthSystem.TakeDamage` aceita `(float, float armorPenetration = 0f, bool isCritical = false)` — compila, mas armorPenetration e crit ficam zerados. Quando migrados para NGO, devem calcular armor pen e crit como `MeleeCombatSystem` e `CuttingBladeLogic` fazem.

### 29. NuvemDeTintaLogic e BombaSprayProjectile usam Destroy() e SendMessage()
**Arquivos:** `NuvemDeTintaLogic.cs:23,79`, `BombaSprayProjectile.cs:59,72`
**Severidade:** Alta (bloqueia multiplayer para Polvo)
**Descricao:** `Destroy(gameObject)` em vez de `Despawn()`, `SendMessage("SetBlinded")` em vez de chamada direta, `Debug.Log` spam nas linhas 45 e 66 de NuvemDeTintaLogic. Parte do problema geral de Dragao/Polvo nao migrados (Alerta #19).

---

## Convencoes para Novos Alertas

Ao adicionar alertas futuros, seguir o formato:
1. Titulo descritivo
2. Caminho do arquivo com linha
3. Severidade: Critica / Alta / Media / Baixa
4. Descricao do problema
5. Correcao sugerida com snippet de codigo
