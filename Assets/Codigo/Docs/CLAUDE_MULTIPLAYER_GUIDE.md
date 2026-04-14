# Claude - Orquestrador, Verificador e Debug
# Projeto: ExoBeasts V3 - Migracacao Multiplayer NGO

## Seu Papel

Voce eh o **orquestrador** da migracao multiplayer do ExoBeasts V3.
Suas responsabilidades:
1. **Coordenar** a ordem de execucao das fases e sprints
2. **Verificar** cada script migrado pelo Gemini antes de aprovar
3. **Debugar** erros de compilacao e runtime
4. **Garantir** que singleplayer nao quebre
5. **Validar** padroes NGO em cada alteracao

---

## Contexto do Projeto

- **Engine:** Unity 6 com Netcode for GameObjects (NGO) 1.12.0
- **Transporte:** Unity Transport 2.4.0 (UDP P2P)
- **Auth:** Epic Online Services (EOS) - Device ID
- **Lobby:** EOS Lobby Service
- **Modelo:** P2P com Host (1 jogador eh servidor+cliente, outros sao clientes)
- **Max jogadores:** 4
- **Arquitetura dual-mode:** Singleplayer = Host local sem clientes remotos

---

## Regras de Ouro NGO (Validar em TODA alteracao)

### 1. Ciclo de Vida
- `OnNetworkSpawn()` substitui `Start()` para tudo que depende de rede
- `IsServer`, `IsOwner`, `IsClient` sao **INVALIDOS** antes de `OnNetworkSpawn()`
- `OnNetworkDespawn()` substitui `OnDestroy()` para cleanup de rede
- Coroutines em objetos despawnados = ERRO. Verificar `IsSpawned` ou usar `destroyCancellationToken`

### 2. Autoridade
- **Servidor controla:** vida, dano, spawn/despawn, economia, waves, estado de jogo
- **Owner controla:** input, movimento local, mira, camera
- **Todos recebem:** animacoes (via NetworkAnimator), visuais (via ClientRpc), sons

### 3. NetworkVariables vs RPCs
- **NetworkVariable:** dados persistentes que late-joiners precisam (vida, wave atual, dinheiro)
- **RPC:** eventos unicos que nao precisam ser repetidos (som de tiro, flash de hit)
- **ServerRpc:** cliente pede algo ao servidor (dano, construcao, compra)
- **ClientRpc:** servidor avisa clientes (efeito visual, som, animacao)

### 4. Proibicoes Absolutas
- NUNCA usar `SceneManager.LoadScene()` em sessao multiplayer → usar `NetworkManager.SceneManager.LoadScene()`
- NUNCA usar `Time.timeScale = 0` em multiplayer → pause eh visual-only
- NUNCA usar `Destroy()` em NetworkObject → usar `Despawn()`
- NUNCA usar `FindGameObjectWithTag("Player")` para achar jogador → usar PlayerRegistry ou `SpawnManager.GetLocalPlayerObject()`
- NUNCA alterar NetworkVariable antes de `OnNetworkSpawn()`
- NUNCA chamar `Start()` com logica de rede → mover para `OnNetworkSpawn()`

---

## Checklist de Verificacao por Script

Quando o Gemini entregar um script migrado, verifique:

### Compilacao
- [ ] Heranca correta (NetworkBehaviour onde necessario)
- [ ] `using Unity.Netcode;` presente
- [ ] NetworkVariables declaradas com permissoes corretas
- [ ] ServerRpc tem `[ServerRpc]` ou `[ServerRpc(RequireOwnership = false)]`
- [ ] ClientRpc tem `[ClientRpc]`
- [ ] Sem erros de tipo (ex: acessar .Value em NetworkVariable)

### Logica de Rede
- [ ] `Start()` migrado para `OnNetworkSpawn()` onde necessario
- [ ] Input protegido com `if (!IsOwner) return;`
- [ ] Dano/estado protegido com `if (!IsServer) return;`
- [ ] Cameras/AudioListeners desativados para `!IsOwner`
- [ ] FindObjectOfType removido ou substituido
- [ ] SceneManager.LoadScene substituido por versao NGO

### Cenarios de Teste Mental
Para cada script, simule mentalmente:
1. **2 jogadores entram:** ambos veem o personagem do outro?
2. **Jogador A atira:** Jogador B ve o tiro + ouve o som?
3. **Inimigo morre:** ambos veem a morte + popup de dano?
4. **Jogador desconecta:** host continua funcionando?
5. **Singleplayer:** tudo funciona identico sem clientes?

---

## Ordem de Execucao (Sprints)

### Sprint 1: Fundacao
**Objetivo:** GameModeManager + BootstrapScene + Menu Solo/Online
**Validar:** Menu funciona, singleplayer carrega como antes, NetworkManager persiste entre cenas

### Sprint 2: Prefab do Jogador
**Objetivo:** Migrar os 10 scripts core do jogador
**Ordem de migracao (respeitar dependencias):**
1. CameraController.cs + ThirdPersonCamera.cs (independentes)
2. PlayerMovement.cs (depende de camera)
3. PlayerHealthSystem.cs (depende de movimento para respawn)
4. PlayerShooting.cs (depende de saude para dano)
5. MeleeCombatSystem.cs (depende de saude para dano)
6. PlayerCombatManager.cs (depende de shooting + melee)
7. CommanderAbilityController.cs (depende de saude + combate)
8. ProjectileVisual.cs (depende de shooting)
**Validar MPPM:** dois jogadores na cena, testar cada sistema

### Sprint 3: Inimigos e Waves
**Objetivo:** Inimigos sincronizados + HordeManager unificado
**Ordem:**
1. EnemyHealthSystem.cs (base)
2. EnemyCombatSystem.cs (depende de saude)
3. EnemyPoolManager.cs (INetworkPrefabInstanceHandler)
4. Unificar HordeManager + NetworkedHorde
5. EnemyController targeting para multiplos jogadores
**Validar:** Waves spawnam, inimigos perseguem jogador correto, morrem sincronizado

### Sprint 4: Construcao e Economia
**Objetivo:** BuildManager com ServerRpc + CurrencyManager vinculado
**Validar:** Construir torre, custo deduzido para todos, torre aparece para todos

### Sprint 5: UI e Gerenciadores
**Objetivo:** HUD local, UIManager sem timeScale, PauseControl visual
**Validar:** HUD mostra dados do jogador LOCAL, pause nao afeta outros

### Sprint 6: Habilidades (60+ scripts)
**Objetivo:** Aplicar padrao universal ServerRpc/ClientRpc
**Migrar por personagem:** Raposa -> Coruja -> Dragao -> Polvo
**Validar:** Cada habilidade testada em MPPM

### Sprint 7: Utilitarios e Polish
**Objetivo:** Scripts menores + substituir todos FindObjectOfType/SceneManager restantes
**Validar:** Teste end-to-end completo

### Sprint 8: Integracao Final
**Objetivo:** Fluxo completo Menu -> Lobby -> Jogo -> Vitoria/Derrota
**Validar:** Multiplayer completo + Singleplayer completo + Desconexao

---

## Debugging Multiplayer - Guia Rapido

### Erros Comuns e Solucoes

**"NetworkVariable is written to by a non-server"**
→ Verificar WritePermission da NetworkVariable. Usar `NetworkVariableWritePermission.Server` e alterar apenas em `if (IsServer)`

**"RPC called on non-spawned object"**
→ Verificar que o objeto tem NetworkObject e esta spawnado. Mover logica para `OnNetworkSpawn()`

**"Multiple AudioListeners in scene"**
→ Desativar AudioListener em jogadores remotos (`if (!IsOwner)`)

**Jogador "desliza" parado na tela dos outros**
→ Falta NetworkAnimator/ClientNetworkAnimator no prefab

**Todos os inimigos perseguem o Host**
→ `FindGameObjectWithTag("Player")` pegando o primeiro. Usar PlayerRegistry com lista de todos

**Camera "piscando" entre jogadores**
→ Cameras de jogadores remotos nao desativadas. Adicionar `if (!IsOwner)` no CameraController

**Dano aplicado multiplas vezes**
→ Todos os clientes chamando TakeDamage. Proteger com `if (!IsServer)` ou `if (!IsOwner)`

**Objeto de rede destruido no cliente**
→ Usar `Despawn()` no servidor, nao `Destroy()`. Para pools, implementar `INetworkPrefabInstanceHandler`

---

## Decisoes Arquiteturais Tomadas

1. **HordeManager unificado:** Toda logica de waves fica no HordeManager.cs (deletar NetworkedHorde.cs)
2. **Vida no PlayerHealthSystem:** NetworkVariable de vida fica no PlayerHealthSystem (remover duplicata do NetworkedPlayerController)
3. **Documentacao em Assets/:** Arquivos de orquestracao ficam dentro do projeto Unity
4. **Start-As-Host:** Singleplayer roda como Host local. Nenhum codigo especial para modo offline
5. **Projeteis visuais locais:** Balas rapidas nao sao NetworkObjects. Pool local por cliente
6. **Owner-authoritative movement:** Jogador controla proprio movimento via ClientNetworkTransform
