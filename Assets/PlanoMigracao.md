# Plano de Migração Multiplayer - ExoBeasts V3

## Contexto

O ExoBeasts V3 é um tower defense em terceira pessoa (Unity 6, NGO 1.12.0) que precisa ser portado para multiplayer P2P (até 4 jogadores) mantendo modo singleplayer. O projeto já possui infraestrutura multiplayer completa (EOS Auth, Lobby, NetworkBootstrap, Sync wrappers) na pasta `Assets/Codigo/Multiplayer/`, mas os **scripts core do jogo** ainda não foram migrados. A documentação PDF (DocMultiExo.pdf, 180 páginas) fornece instruções detalhadas por script. Este plano organiza e prioriza essa migração.

**Arquitetura alvo:** Start-As-Host (singleplayer = Host sem clientes remotos). Todo script roda com NetworkManager ativo em ambos os modos.

---

## FASE 0: Fundação Dual-Mode (Singleplayer/Multiplayer Switch)

### Objetivo
Criar um sistema que permita o jogo rodar em dois modos usando a mesma base de código NGO.

### Arquivos a criar/modificar

**CRIAR: `Assets/Codigo/Managers/GameModeManager.cs`**
```csharp
// Singleton persistente (DontDestroyOnLoad)
// Enum GameMode { Singleplayer, Multiplayer }
// No Singleplayer: NetworkManager.StartHost() silenciosamente (sem lobby)
// No Multiplayer: fluxo completo (EOS Auth → Lobby → StartHost/Client)
// Método: StartSingleplayer() → carrega cena direto como Host local
// Método: StartMultiplayer() → redireciona para fluxo de Lobby
// NetworkVariable ou flag estática: public static GameMode CurrentMode
```

**MODIFICAR: `Assets/Codigo/Managers/MenuManager.cs`**
- Adicionar botões "Jogar Solo" e "Jogar Online" no menu principal
- "Jogar Solo" → GameModeManager.StartSingleplayer() → cena de seleção → jogo
- "Jogar Online" → GameModeManager.StartMultiplayer() → LobbyScene

**MODIFICAR: `Assets/Codigo/Managers/Saves/GameSetupManager.cs`**
- O spawn de jogador deve usar `OnClientConnectedCallback` em vez de `Start()`
- Ler personagem do Connection Approval Payload (multiplayer) ou GameDataManager (solo)
- Usar array de `Transform[] spawnPoints` para evitar sobreposição de jogadores
- Registrar jogador no `PlayerRegistry` após spawn

### Fluxo de cenas (reorganização)
```
MenuScene → [Singleplayer] → EscolherPersonagem → CenaMapaTeste (como Host local)
MenuScene → [Multiplayer]  → LobbyScene (Auth+Lobby) → CenaMapaTeste (como Host/Client)
```

### Problemas previstos
- NetworkManager precisa existir antes de qualquer cena de jogo → usar cena Bootstrap persistente
- GameDataManager.Instance.equipeSelecionada é local → no multiplayer, cada cliente envia seu ID via Connection Approval Payload
- Time.timeScale = 0 no pause não funciona em multiplayer → pause deve ser visual/input-only

---

## FASE 1: Scripts Core do Jogador (PRIORIDADE MÁXIMA)

### 1.1 PlayerMovement.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/PlayerMovement.cs`
**Estado:** Já é NetworkBehaviour com IsOwner

**Alterações necessárias:**
- [ ] Mover inicializações de `Start()` para `OnNetworkSpawn()`
- [ ] Refatorar `Update()`: separar lógica de INPUT (IsOwner only) de lógica VISUAL (todos)
  - Input/física: continua atrás de `if (!IsOwner) return;`
  - Animator params (SetFloat, SetBool): devem rodar para TODOS via NetworkAnimator
  - Aim target position: criar `NetworkVariable<Vector3> netAimTarget` (WritePermission.Owner)
  - Model pivot rotation: criar `NetworkVariable<float> netModelYRotation` (WritePermission.Owner)
- [ ] Adicionar `ClientNetworkAnimator` no prefab (em vez de NetworkAnimator, pois é owner-auth)
- [ ] Trocar `animator.SetTrigger("Jump")` por `GetComponent<NetworkAnimator>().SetTrigger("Jump")`
- [ ] Sincronizar estados visuais: `NetworkVariable<bool> isMoving`, `isDashing`, `isFloating`
- [ ] FMOD footsteps: vincular a Animation Events (sincroniza via NetworkAnimator) OU usar NetworkVariable<bool> isMoving para controlar Play/Stop
- [ ] Remover `FindObjectOfType<CameraController>()` → injetar via OnNetworkSpawn

**Componentes no Prefab do Jogador:**
- ClientNetworkTransform (owner-authoritative position sync)
- ClientNetworkAnimator (owner-authoritative animation sync)
- NetworkObject (raiz do prefab)
- NetworkRigidbody (se usar Rigidbody) OU desabilitar para CharacterController

### 1.2 PlayerHealthSystem.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/PlayerHealthSystem.cs`
**Estado:** Já é NetworkBehaviour com NetworkVariable<float> currentHealth

**Alterações necessárias:**
- [ ] Mover `Start()` → `OnNetworkSpawn()` com `if (IsServer)` para init de valores
- [ ] Converter buffs para NetworkVariables: `speedMultiplier`, `damageMultiplier`, `damageResistance`
- [ ] Refatorar `Die()`: servidor reseta vida + chama `RespawnClientRpc()`
  - Dentro do ClientRpc: `if (IsOwner)` desliga CharacterController, teleporta, reativa
- [ ] Passive `OnEquip()`: mover para `OnNetworkSpawn()`, avaliar se altera stats (server-only) ou visual (todos)
- [ ] Corrotinas de buff: manter no servidor, adicionar ClientRpc para VFX de buff

### 1.3 PlayerShooting.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/PlayerShooting.cs`
**Estado:** Já é NetworkBehaviour com IsOwner e RequestDealDamageServerRpc

**Alterações necessárias:**
- [ ] Mover `Start()` → `OnNetworkSpawn()`
- [ ] Criar `NetworkVariable<Vector3> netAimTarget` (WritePermission.Owner) para mira
- [ ] Criar cadeia de sincronização visual para tiro:
  - Owner atira localmente (zero lag) → `ShootVisualServerRpc(direction)`
  - Servidor repassa → `ShootVisualClientRpc(direction)`
  - ClientRpc: `if (!IsOwner)` toca animator.SetTrigger("Shoot"), PlayShootSound(), SpawnProjectile visual
- [ ] Mesma cadeia para recarga: `ReloadServerRpc()` → `ReloadClientRpc()`
- [ ] **CONECTAR** ProjectileVisual.OnTriggerEnter ao RequestDealDamageServerRpc (está desconectado!)
- [ ] Sincronizar `isReloading` via NetworkVariable ou ClientRpc

### 1.4 MeleeCombatSystem.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/MeleeCombatSystem.cs`
**Estado:** MonoBehaviour → PRECISA virar NetworkBehaviour

**Alterações necessárias:**
- [ ] Mudar herança: `MonoBehaviour` → `NetworkBehaviour`
- [ ] Adicionar `if (!IsOwner) return;` em `OnFire()` e `Update()`
- [ ] Refatorar `DetectHits()` (chamado por Animation Events):
  - FMOD PlayOneShot → toca para TODOS (animação sincronizada)
  - `if (!IsOwner) return;` DEPOIS do som
  - Physics.OverlapSphere → apenas Owner calcula
  - Substituir `TakeDamage` direto por `RequestMeleeDamageServerRpc(enemyNetworkObjectId, damage)`
- [ ] Criar `[ServerRpc] RequestMeleeDamageServerRpc(ulong targetId, float damage)`

### 1.5 PlayerCombatManager.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/PlayerCombatManager.cs`
**Estado:** MonoBehaviour → PRECISA virar NetworkBehaviour

**Alterações necessárias:**
- [ ] Mudar herança → NetworkBehaviour
- [ ] `Start()` → `OnNetworkSpawn()`
- [ ] Criar `NetworkVariable<CombatType> netCombatType` (server-write)
- [ ] Usar `OnValueChanged` para atualizar visuais (modelos 3D SetActive) em TODOS os clientes
- [ ] `Update()` com `if (!IsOwner) return;` para input de troca
- [ ] Troca de arma: `[ServerRpc] RequestSwitchWeaponServerRpc(CombatType newType)`
- [ ] Ativação de scripts de ataque (.enabled) apenas para Owner

### 1.6 CameraController.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/CameraController.cs`
**Estado:** MonoBehaviour → PRECISA virar NetworkBehaviour

**Alterações necessárias:**
- [ ] Mudar herança → NetworkBehaviour
- [ ] `OnNetworkSpawn()`: `if (!IsOwner)` → desativar Camera, AudioListener, CinemachineCameras, `this.enabled = false`
- [ ] Impede "câmera esquizofrenia" (múltiplas câmeras ativas)

### 1.7 ThirdPersonCamera.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/ThirdPersonCamera.cs`
**Estado:** MonoBehaviour → PRECISA virar NetworkBehaviour

**Alterações necessárias:**
- [ ] Mudar herança → NetworkBehaviour
- [ ] `Start()` → `OnNetworkSpawn()` com `if (!IsOwner)` desativa Camera + AudioListener + this.enabled
- [ ] Toda matemática de câmera (LateUpdate, zoom, transição 1a/3a pessoa) continua intacta

### 1.8 CommanderAbilityController.cs
**Caminho:** `Assets/Codigo/Char scripts/JP/CommanderAbilityController.cs`
**Estado:** MonoBehaviour → PRECISA virar NetworkBehaviour

**Alterações necessárias:**
- [ ] Mudar herança → NetworkBehaviour
- [ ] `if (!IsOwner) return;` nos inputs (Q, E, X)
- [ ] Cada ativação de habilidade → `[ServerRpc]` que valida cooldown no servidor
- [ ] Efeitos visuais de habilidade → `[ClientRpc]` para todos verem
- [ ] Ultimate charge → `NetworkVariable<float>` para HUD de todos

### 1.9 ProjectilePool.cs
**Caminho:** `Assets/Codigo/Char scripts/Player/ProjectilePool.cs`
**Estado:** MonoBehaviour Singleton

**Alterações necessárias:**
- [ ] Manter como MonoBehaviour (projéteis são visuais locais, não NetworkObjects)
- [ ] Cada cliente tem seu próprio pool local — correto para projéteis rápidos
- [ ] Se decidir fazer projéteis lentos (foguete, bola de fogo) como NetworkObject → usar `INetworkPrefabInstanceHandler`

### 1.10 ProjectileVisual.cs
**Caminho:** `Assets/Codigo/ProjectileVisual.cs`
**Estado:** MonoBehaviour

**Alterações necessárias:**
- [ ] No `OnTriggerEnter`: envelopar dano com `if (NetworkManager.Singleton.IsServer)`
- [ ] OU melhor: Owner detecta hit → chama `PlayerShooting.RequestDealDamageServerRpc()`
- [ ] Pool normal continua funcionando (projéteis não são NetworkObjects)

---

## FASE 2: Sistema de Inimigos

### 2.1 EnemyController.cs
**Caminho:** `Assets/Codigo/Enemy/EnemyControler.cs`
**Estado:** MonoBehaviour com NavMeshAgent (já tem null-checks robustos)

**Alterações necessárias:**
- [ ] IA roda APENAS no servidor (NetworkedEnemy já faz `enemyController.enabled = runAI`)
- [ ] `FindFirstObjectByType<ObjectiveHealthSystem>()` → cachear referência no `InitializeEnemy()`
- [ ] Múltiplos jogadores: `FindGameObjectWithTag("Player")` → trocar para `List<Transform>` de todos os jogadores conectados, usar Physics.OverlapSphere ou PlayerRegistry para achar o mais próximo
- [ ] Verificar que todos os status effects (slow, root, knockback) funcionam server-side

### 2.2 EnemyHealthSystem.cs
**Caminho:** `Assets/Codigo/Enemy/EnemyHealthSystem.cs`
**Estado:** MonoBehaviour → precisa ter dano server-authoritative

**Alterações necessárias:**
- [ ] Vida controlada pelo servidor via `NetworkedEnemy.NetworkHealth`
- [ ] `TakeDamage()` deve ser chamado apenas pelo servidor
- [ ] Hit flash visual: `[ClientRpc]` para todos verem o flash
- [ ] Damage popup: spawn local em cada cliente ao receber ClientRpc de hit
- [ ] WorldSpaceEnemyUI: ler `NetworkHealth.OnValueChanged` para barra de vida

### 2.3 EnemyCombatSystem.cs
**Caminho:** `Assets/Codigo/Enemy/EnemyCombatSystem.cs`
**Estado:** MonoBehaviour

**Alterações necessárias:**
- [ ] Detecção de colisão roda apenas no servidor (já que EnemyController é server-only)
- [ ] Dano no jogador: servidor chama `PlayerHealthSystem.TakeDamage()` diretamente (ambos no servidor)
- [ ] Animação de ataque: sincronizada via NetworkAnimator no inimigo

### 2.4 EnemyPoolManager.cs
**Caminho:** `Assets/Codigo/Enemy/EnemyPoolManager.cs`
**Estado:** MonoBehaviour Singleton com Netcode

**Alterações necessárias:**
- [ ] Implementar `INetworkPrefabInstanceHandler` para que clientes não destruam inimigos no Despawn
- [ ] Registrar: `NetworkManager.Singleton.PrefabHandler.AddHandler(enemyPrefab, this);`
- [ ] `InitializePool()` deve rodar em TODOS (servidor E clientes) — corrigir bug atual onde cliente não inicializa
- [ ] Trocar `List<GameObject>` por `Queue<GameObject>` para performance
- [ ] Spawn: servidor faz `GetPooledEnemy()` + `NetworkObject.Spawn(true)`
- [ ] Despawn: servidor faz `NetworkObject.Despawn(false)` → handler do cliente faz SetActive(false)

### 2.5 EnemyDataSO.cs
**Caminho:** `Assets/Codigo/Enemy/EnemyDataSO.cs`
**Estado:** ScriptableObject — NÃO PRECISA MUDAR

### 2.6 WorldSpaceEnemyUI.cs
**Caminho:** `Assets/Codigo/Enemy/WorldSpaceEnemyUI.cs`
**Estado:** MonoBehaviour

**Alterações necessárias:**
- [ ] Vincular a `NetworkedEnemy.NetworkHealth.OnValueChanged` para atualizar barra de vida
- [ ] Sem herança de NetworkBehaviour necessária

---

## FASE 3: Gerenciadores de Jogo

### 3.1 HordeManager.cs
**Caminho:** `Assets/Codigo/Managers/HordeManager.cs`
**Estado:** Já é NetworkBehaviour

**Alterações necessárias:**
- [ ] `FindGameObjectWithTag("Player")` → `List<Transform>` de todos os jogadores via PlayerRegistry
- [ ] `currentHorde` → `NetworkVariable<int>` (late-join safety)
- [ ] `SceneManager.LoadScene("Win")` → `NetworkManager.Singleton.SceneManager.LoadScene("Win", LoadSceneMode.Single)`
- [ ] Spawn de inimigos: verificar integração com EnemyPoolManager + NetworkObject.Spawn

### 3.2 BuildManager.cs
**Caminho:** `Assets/Codigo/Managers/BuildManager.cs`
**Estado:** MonoBehaviour Singleton

**Alterações necessárias:**
- [ ] Ghost preview: continua local (zero lag visual)
- [ ] `PlaceBuilding()` → `[ServerRpc] RequestBuildServerRpc(int buildableID, Vector3 position, ulong clientId)`
- [ ] Servidor valida: custo em Geodites, limite de traps, grid válido
- [ ] Servidor faz `Instantiate()` + `NetworkObject.Spawn()`
- [ ] `activeTrapCounts` mantido pelo servidor
- [ ] CurrencyManager integrado via NetworkedCurrency

### 3.3 CurrencyManager.cs
**Caminho:** `Assets/Codigo/Managers/CurrencyManager.cs`
**Estado:** MonoBehaviour Singleton

**Alterações necessárias:**
- [ ] Vincular a `NetworkedCurrency` já existente (NetworkVariables TeamGeodites, TeamDarkEther)
- [ ] UI lê `NetworkVariable.OnValueChanged` para atualizar texto
- [ ] Compras: cliente envia ServerRpc → servidor valida → atualiza NetworkVariable
- [ ] Remover alteração local de valores — tudo passa pelo servidor

### 3.4 UIManager.cs
**Caminho:** `Assets/Codigo/Managers/UIManager.cs`
**Estado:** MonoBehaviour Singleton — CONTINUA MonoBehaviour

**Alterações necessárias:**
- [ ] Remover `Time.timeScale = 0` do pause → pause visual/input-only
- [ ] Timer de jogo: ler de `MatchManager.MatchTime` (NetworkVariable)
- [ ] Vida do objetivo: ler de NetworkVariable do ObjectiveHealthSystem
- [ ] Compras na loja: esperar servidor aprovar antes de atualizar UI

### 3.5 PlayerHUD.cs
**Caminho:** `Assets/Codigo/Managers/PlayerHUD.cs`
**Estado:** MonoBehaviour

**Alterações necessárias:**
- [ ] Remover `FindGameObjectWithTag("Player")` do Update
- [ ] Implementar Injeção de Dependência: PlayerHUD vira Singleton
- [ ] No `PlayerHealthSystem.OnNetworkSpawn()`: `if (IsOwner) PlayerHUD.Instance.RegistrarJogador(this, ...)`
- [ ] HUD só mostra dados do jogador LOCAL
- [ ] Usar `NetworkManager.Singleton.SpawnManager.GetLocalPlayerObject()` como fallback

### 3.6 PauseControl.cs
**Caminho:** `Assets/Codigo/Managers/PauseControl.cs`
**Estado:** MonoBehaviour com `static isPaused`

**Alterações necessárias:**
- [ ] Multiplayer: pause é VISUAL ONLY (não afeta Time.timeScale)
- [ ] `isPaused` fica local por cliente (cada um pode pausar independentemente)
- [ ] Bloquear input do jogador quando pausado, mas o jogo continua rodando

### 3.7 ObjectiveHealthSystem.cs
**Caminho:** `Assets/Codigo/Managers/ObjectiveHealthSystem.cs`
**Estado:** MonoBehaviour → PRECISA virar NetworkBehaviour

**Alterações necessárias:**
- [ ] Mudar herança → NetworkBehaviour
- [ ] `health` → `NetworkVariable<float>` (server-write)
- [ ] `TakeDamage()` → apenas servidor pode chamar
- [ ] `OnValueChanged` → atualiza UI para todos os clientes

### 3.8 GameDataManager.cs / GameSetupManager.cs / SelecaoManager.cs
**Estado:** MonoBehaviour Singletons — CONTINUAM MonoBehaviour

**Alterações necessárias:**
- [ ] GameDataManager: dados de save/progresso são LOCAIS (cada máquina tem o seu)
- [ ] GameSetupManager: spawn de jogador refatorado (ver FASE 0)
- [ ] SelecaoManager: seleção local → envia ID via Connection Approval Payload no multiplayer

### 3.9 TopDownCameraManager.cs
**Estado:** MonoBehaviour Singleton — CONTINUA MonoBehaviour

**Alterações necessárias:**
- [ ] Adicionar `SetCameraTarget(Transform localPlayerTransform)` para vincular ao jogador local
- [ ] Chamado no `OnNetworkSpawn()` do PlayerMovement: `if (IsOwner) TopDownCameraManager.Instance.SetCameraTarget(transform)`

### 3.10 MenuManager.cs
**Alterações necessárias:**
- [ ] Integrar botões Singleplayer/Multiplayer via GameModeManager
- [ ] `SceneManager.LoadScene` → usar `NetworkManager.SceneManager.LoadScene` quando em sessão

---

## FASE 4: Torres e Armadilhas

### 4.1 TowerController.cs
**Alterações necessárias:**
- [ ] IA da torre (targeting, shooting) roda apenas no servidor
- [ ] Animações de tiro: sincronizadas via NetworkAnimator
- [ ] Dano aplicado pelo servidor
- [ ] Stats lidos do CharacterBase (ScriptableObject — local, read-only)

### 4.2 TowerAbilitySystem.cs / TowerBehavior.cs / Upgrade paths
**Alterações necessárias:**
- [ ] Upgrades: cliente solicita via ServerRpc → servidor valida custo → NetworkedBuilding.Level++
- [ ] Efeitos de habilidade (aura, splash): servidor calcula → ClientRpc para visuais
- [ ] NetworkedBuilding já existe e sincroniza Type, Level, Health, IsActive

### 4.3 TrapLogicBase.cs
**Alterações necessárias:**
- [ ] Trigger de dano: apenas servidor
- [ ] Destruir/Vender: `NetworkObject.Despawn()` em vez de `Destroy()`
- [ ] Contagem de traps ativas: servidor mantém via BuildManager

### 4.4 GridPlacement.cs — NÃO PRECISA MUDAR
- Cálculos matemáticos puramente locais
- Preview/Ghost é local
- Validação final acontece no BuildManager (servidor)

---

## FASE 5: Habilidades de Personagem (60+ scripts)

### Padrão universal para TODAS as habilidades:
```
1. Input (OnActivate) → if (!IsOwner) return;
2. Owner chama → [ServerRpc] RequestAbilityServerRpc(abilityId, params)
3. Servidor valida cooldown, recursos → executa lógica de dano/buff
4. Servidor chama → [ClientRpc] AbilityVisualClientRpc(params)
5. Todos os clientes: VFX, SFX, animações
6. Dano em inimigos: servidor usa NetworkObjectId para encontrar e aplicar
```

### CharacterBase.cs (ScriptableObject)
- [ ] Remover estado dinâmico (Rastros Progress) → mover para `PlayerProgressManager : NetworkBehaviour`
- [ ] `pontosRastrosDisponiveis` → `NetworkVariable<int>`
- [ ] `habilidadesDesbloqueadas` → `NetworkList<FixedString32Bytes>` ou IDs numéricos
- [ ] `ResetarRastros()` → `[ServerRpc] RequestResetRastrosServerRpc()`

### Habilidades por personagem (aplicar padrão acima):
- **Raposa (14 scripts):** CuttingBlade, NineTailsDance, PeaceOfMind, + tower behaviors
- **Coruja (21 scripts):** CacadoraNoturna, VooGracioso, PerseguindoPresas, + tower behaviors
- **Dragão (7 scripts):** AquiNao, PosturaBaluarte, TemorSismico
- **Polvo (11 scripts):** BombaSpray, MergulhoTinta, NuvemDeTinta, ObraPrima, PaintSystem

### Scripts de habilidade que NÃO precisam de rede:
- Ability.cs (ScriptableObject base) — dados estáticos
- passivaAbility.cs (ScriptableObject) — dados estáticos
- Tower behavior configs — dados estáticos

---

## FASE 6: Utilitários e Diversos

| Script | Ação | Detalhes |
|--------|------|----------|
| VerificadorQueda.cs | → NetworkBehaviour | Adicionar `if (!IsOwner) return;` no Update. Teleporte local, NGO replica via ClientNetworkTransform |
| DamagePopup.cs | Manter MonoBehaviour | Spawn local em cada cliente quando receber ClientRpc de hit |
| AnimationEventProxy.cs | Manter | Funciona via NetworkAnimator |
| CursorOn.cs | Manter ou → NB | Se no prefab jogador: `if (!IsOwner) return;`. Se em Canvas: sem mudança |
| WinSound.cs | → NetworkBehaviour | Servidor sorteia índice → `PlayVictoryMusicClientRpc(index)` |
| LoseSound.cs | → NetworkBehaviour | Servidor sorteia índice → `PlayDefeatMusicClientRpc(index)` |
| VolumeManager.cs | Manter | 100% local (PlayerPrefs) |
| MusicManager.cs | Manter | Singleton local. Outros scripts chamam via ClientRpc |
| GerenciadorDeSomGlobal.cs | Manter | 100% local |
| PreyMarkLogic.cs | → NetworkBehaviour | `if (!IsServer) return;` no StartEffect. Debuffs server-only |
| SpawnPath.cs | Manter | Dados estáticos de rota |

---

## FASE 7: Integração e Hierarquia de Cenas

### Hierarquia de cenas proposta:
```
1. BootstrapScene (persistente, DontDestroyOnLoad)
   └─ NetworkManager + UnityTransport
   └─ GameModeManager
   └─ EOSManagerWrapper (só carrega no modo Multiplayer)
   └─ SessionManager
   └─ MusicManager

2. MenuScene
   └─ MenuManager (botões Solo/Online)
   └─ VolumeManager

3. LobbyScene (apenas Multiplayer)
   └─ EOSAuthenticator
   └─ LobbyManager
   └─ LobbyUI (substituir LobbyPlaceholderUI por UI real)

4. EscolherPersonagem
   └─ SelecaoManager
   └─ (seleção local, envia ID ao conectar)

5. CenaMapaTeste (cena de jogo principal)
   └─ HordeManager (NetworkBehaviour)
   └─ EnemyPoolManager
   └─ BuildManager
   └─ CurrencyManager → vinculado a NetworkedCurrency
   └─ ObjectiveHealthSystem (NetworkBehaviour)
   └─ MatchManager
   └─ GameServerManager
   └─ PlayerRegistry
   └─ UIManager + PlayerHUD (locais)
   └─ SpawnPaths, SpawnPoints
```

### Vinculação com multiplayer existente:
- [ ] `NetworkedPlayerController`: REMOVER duplicata de NetworkHealth → vida fica no `PlayerHealthSystem` (já tem NetworkVariable)
- [ ] `NetworkedHorde`: UNIFICAR com `HordeManager` → mover toda lógica de rede para dentro do HordeManager e deletar NetworkedHorde
- [ ] `NetworkedBuilding` ↔ `TowerController` (NetworkedBuilding wrapa TowerController)
- [ ] `NetworkedCurrency` ↔ `CurrencyManager` (CurrencyManager lê de NetworkedCurrency)
- [ ] `PlayerNetworkSetup` ↔ `CameraController/PlayerMovement` (desabilita componentes remotos)
- [ ] `NetworkedEnemy` ↔ `EnemyController` (já vinculado — server-only AI)

---

## Problemas Previstos e Mitigações

### Críticos
1. **Singleton Hell (35 singletons):** Em multiplayer, cada cliente tem suas próprias instâncias. Singletons de rede devem setar Instance em `OnNetworkSpawn()`, não `Awake()`.
2. **FindObjectOfType (217 ocorrências):** Vai pegar objetos errados com múltiplos jogadores. Substituir por referências injetadas, PlayerRegistry, ou `SpawnManager.GetLocalPlayerObject()`.
3. **Start() vs OnNetworkSpawn():** Toda inicialização que depende de rede deve ir para `OnNetworkSpawn()`. `IsServer`/`IsOwner` são inválidos antes do spawn.
4. **SceneManager.LoadScene:** NUNCA usar o padrão do Unity em sessão multiplayer. Sempre `NetworkManager.SceneManager.LoadScene()`.
5. **Time.timeScale = 0:** Proibido em multiplayer. Pause é visual-only.

### Moderados
6. **CharacterController + ClientNetworkTransform:** Podem conflitar. Testar cuidadosamente. Desabilitar CharacterController em jogadores remotos.
7. **FMOD em múltiplos clientes:** Sons locais tocando para todos. Vincular a Animation Events ou NetworkVariable<bool>.
8. **Object Pooling:** Pool padrão não funciona com NGO. Implementar `INetworkPrefabInstanceHandler`.
9. **Coroutines em NetworkBehaviour:** Coroutines em objetos despawnados causam erro. Usar `destroyCancellationToken` com Awaitable ou verificar `IsSpawned`.
10. **Prefabs sem NetworkObject:** Todo prefab spawnado em rede precisa de NetworkObject na raiz. Verificar todos os prefabs de jogador, inimigo e torre.

### Menores
11. **Random.Range dessincronizado:** Cada cliente sorteia diferente. Para eventos globais, servidor sorteia e envia via ClientRpc.
12. **Physics.OverlapSphere em clientes:** Detecção de hit roda localmente. Para dano, servidor deve validar.
13. **NetworkAnimator triggers:** `SetTrigger` deve ser chamado via NetworkAnimator, não Animator direto.

---

## Verificação e Testes

### Checklist por fase:
1. **Após cada script migrado:** Testar em MPPM (Multiplayer Play Mode) com 2 instâncias
2. **Verificar:** IsOwner bloqueia input em jogador remoto? Animações sincronizam? Dano é server-only?
3. **Late-join:** Novo jogador entrando mid-game recebe estado correto via NetworkVariables?
4. **Singleplayer:** Modo solo ainda funciona identicamente? (Host local sem clientes)
5. **Desconexão:** Cliente saindo não crasha o jogo do host?

### Testes MPPM:
- Abrir Unity com MPPM (2 virtual players)
- Player 1: StartHost
- Player 2: StartClient
- Verificar: ambos veem o personagem do outro? Animações? Tiros? Dano?
- Trocar para singleplayer e verificar que tudo funciona igual

---

## Arquivos de Orquestração (a criar em Assets/)

### Claude.md — Orquestrador/Verificador/Debug
**Local:** `Assets/Claude.md`

Conteúdo: Instruções para o Claude como orquestrador da migração. Responsável por:
- Verificar cada script migrado pelo Gemini
- Debugar erros de compilação e runtime
- Validar que o padrão NGO está correto (IsOwner, ServerRpc, ClientRpc)
- Rodar testes mentais de cenários multiplayer
- Garantir que singleplayer não quebrou
- Coordenar ordem de execução das fases

### Gemini.md — Executor de alterações diretas
**Local:** `Assets/Gemini.md`

Conteúdo: Instruções detalhadas para o Gemini executar as alterações em cada script. Inclui:
- Padrões de código obrigatórios
- Checklist por script com alterações exatas
- Exemplos de código para cada tipo de migração
- Ordem de execução respeitando dependências

---

## Plano de Execução (Ordem de Implementação)

### Sprint 1: Fundação (1-2 dias)
1. Criar `GameModeManager.cs`
2. Criar `BootstrapScene` com NetworkManager persistente
3. Modificar `MenuManager.cs` com botões Solo/Online
4. Testar: menu → singleplayer funciona como antes

### Sprint 2: Prefab do Jogador (3-5 dias)
1. Configurar prefab: adicionar NetworkObject, ClientNetworkTransform, ClientNetworkAnimator
2. Migrar `PlayerMovement.cs` (Start→OnNetworkSpawn, NetworkVariables visuais)
3. Migrar `CameraController.cs` e `ThirdPersonCamera.cs` (desativar para !IsOwner)
4. Migrar `PlayerHealthSystem.cs` (buffs como NetworkVariables, Die→RespawnClientRpc)
5. Migrar `PlayerShooting.cs` (cadeia ShootServerRpc→ShootClientRpc)
6. Migrar `MeleeCombatSystem.cs` (DetectHits com IsOwner, RequestMeleeDamageServerRpc)
7. Migrar `PlayerCombatManager.cs` (NetworkVariable<CombatType>)
8. Migrar `CommanderAbilityController.cs`
9. Testar MPPM: dois jogadores na cena, movimento/tiro/dano sincronizados

### Sprint 3: Inimigos e Waves (2-3 dias)
1. Verificar `NetworkedEnemy` + `EnemyController` integração
2. Migrar `EnemyHealthSystem.cs` (dano server-only, hit flash ClientRpc)
3. Migrar `EnemyPoolManager.cs` (INetworkPrefabInstanceHandler)
4. Unificar `HordeManager` + `NetworkedHorde` em um só script
5. Migrar targeting para múltiplos jogadores (PlayerRegistry)
6. Testar: waves spawnam, inimigos perseguem jogadores corretos, morrem sincronizado

### Sprint 4: Construção e Economia (2 dias)
1. Migrar `BuildManager.cs` (RequestBuildServerRpc)
2. Vincular `CurrencyManager` ↔ `NetworkedCurrency`
3. Migrar `ObjectiveHealthSystem.cs` → NetworkBehaviour
4. Testar: construir torre, custo deduzido para todos, torre aparece para todos

### Sprint 5: UI e Gerenciadores (1-2 dias)
1. Migrar `PlayerHUD.cs` (injeção de dependência)
2. Migrar `UIManager.cs` (remover timeScale, ler NetworkVariables)
3. Migrar `PauseControl.cs` (visual-only)
4. Migrar `GameSetupManager.cs` (OnClientConnectedCallback, spawn points)
5. Testar: HUD mostra dados corretos do jogador local

### Sprint 6: Habilidades (3-5 dias)
1. Migrar `CharacterBase.cs` (extrair estado dinâmico)
2. Aplicar padrão ServerRpc/ClientRpc em cada habilidade (60+ scripts)
3. Migrar por personagem: Raposa → Coruja → Dragão → Polvo
4. Testar cada habilidade em MPPM

### Sprint 7: Utilitários e Polish (1-2 dias)
1. Migrar scripts menores (VerificadorQueda, WinSound, LoseSound, etc.)
2. Migrar `TopDownCameraManager` (SetCameraTarget)
3. Substituir todos os `SceneManager.LoadScene` por versão NGO
4. Substituir todos os `FindObjectOfType` restantes
5. Teste completo end-to-end: menu → lobby → jogo → vitória/derrota

### Sprint 8: Integração Final (2-3 dias)
1. Criar arquivos `Claude.md` e `Gemini.md` com instruções detalhadas
2. Conectar `LobbyScene` → `EscolherPersonagem` → `CenaMapaTeste`
3. Testar fluxo completo multiplayer com MPPM
4. Testar fluxo singleplayer completo
5. Testar desconexão e reconexão
6. Bug fixes e ajustes finais
