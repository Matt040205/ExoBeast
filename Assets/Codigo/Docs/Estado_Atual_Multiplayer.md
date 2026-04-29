# Estado Atual do Multiplayer

Status: canonico
Publico: agentes e devs do multiplayer
Ler primeiro: `Assets/Diretrizes_Multiagente.md`
Nao usar como fonte de verdade: docs historicas, planos antigos e nomes removidos

Documento canonico do multiplayer atual. Leia este arquivo para entender o que existe hoje,
o que mudou em relacao aos docs antigos e quais nomes devem ser tratados como atuais.

## Docs ativos relacionados

- `Assets/Codigo/Multiplayer/README.md` - indice curto
- `Assets/Codigo/Docs/Guia_Setup_Multiplayer_Cenas.md` - setup de cena e prefab
- `Assets/Codigo/Multiplayer/Docs/AUTHENTICATION_GUIDE.md` - login EOS
- `Assets/Codigo/Multiplayer/CREDENTIALS_SETUP.md` - segredos EOS

## Ultima atualizacao: correcoes de input local, build toggle e disputa de PlayerInput (2026-04-29)

- O problema observado no log nao era mais de spawn, auth ou lobby: o host completava login EOS, criava lobby, entrava na partida e o `PlayerNetworkSetup` terminava o setup local, mas o comandante ainda nao respondia aos inputs de gameplay.
- A investigacao mostrou um `PlayerInput` na cena, no objeto `ManagersDaPartida` de `Assets/Scenes/CenaMapaTeste.unity`, com o action `Player/Build` ligado diretamente a `BuildManager.OnBuild`. Esse componente competia com o `PlayerInput` do player local pelo mesmo teclado/mouse.
- O `PlayerInput` do player local tambem passava por um ciclo `disable -> enable` em `PlayerNetworkSetup`, o que podia deixar referencias de `InputAction` cacheadas em estado velho dentro do `LocalPlayerInputBridge`.
- `PauseControl` ja estava mapeado no prefab, mas nao possuia um callback `OnPause(InputAction.CallbackContext)` compativel com o Input System novo.

### Correcoes aplicadas

| Arquivo | O que foi ajustado |
|---|---|
| `Assets/Codigo/Towers/BuildManager.cs` | Desabilita o `PlayerInput` de cena em `Awake()` para evitar disputa de device com o comandante local. O toggle de build passou a ser lido via `LocalPlayerInputBridge` do owner, com fallback para `Keyboard.current.bKey.wasPressedThisFrame`. |
| `Assets/Codigo/Characters/Player/LocalPlayerInputBridge.cs` | Passou a recachear bindings quando o `PlayerInput` muda de estado, incluindo a action `Build`. Tambem ganhou `ConsumeBuildPressed()`, flags de estado para `Build` e refresh explicito apos reset do `PlayerInput`. |
| `Assets/Codigo/Multiplayer/Sync/PlayerNetworkSetup.cs` | Depois do `disable -> enable -> SwitchCurrentActionMap("Player")`, agora chama `RefreshBindingsAfterPlayerInputReset()` no bridge. O sanity-check do owner tambem passou a verificar `devices.Count` e `currentActionMap.enabled`. |
| `Assets/Codigo/Managers/PauseControl.cs` | Ganhou `OnPause(InputAction.CallbackContext)` para receber o evento do action `Player/Pause` que ja estava ligado no prefab. |

### Comportamento novo

- O `BuildManager` nao depende mais do `PlayerInput` da cena para abrir/fechar build mode.
- O jogador local passa a ser a fonte de verdade para input de gameplay, via `LocalPlayerInputBridge`.
- O build toggle agora pode vir do action `Build` do owner ou, se necessario, do teclado `B`.
- O bridge recacheia `Move`, `Sprint`, `Jump`, `Aim`, `Attack`, `Reload`, `Build`, `Ability1`, `Ability2` e `Ultimate` apos reset do `PlayerInput`.
- O sanity-check local agora acusa claramente se o owner estiver com `PlayerInput` sem device pareado, action map ausente ou action map desabilitado.

### Validacao

- `dotnet build Assembly-CSharp.csproj` concluiu com sucesso, com `0 erro(s)` e `0 aviso(s)`.
- O log do host continua mostrando login EOS, criacao de lobby, conexao, spawn do player local e setup final; a mudanca desta rodada foi focada especificamente em corrigir a disputa de input.

## Atualizacao anterior: refactor de autoridade, HUD, traps, habilidades e IA (2026-04-28)

- `ObjectiveHealthSystem` virou a fonte autoritativa da vida da Base e agora publica atualizacoes via `ObjectiveHealthBus`; `PlayerHUD` e `UIManager` apenas escutam eventos.
- `Prefeitura.prefab` agora e um `NetworkObject` de cena para a Base sincronizar vida e derrota corretamente entre host e clientes.
- Foi criada a pasta `Assets/Codigo/Combat/` com `DamageContext`, `DamageFeedbackMode`, `DamageRequest`, `DamageResponse` e `IDamageInterceptor` para padronizar validacao, bloqueio e feedback de dano.
- `EnemyHealthSystem` e `NetworkedEnemy` passaram a usar contexto de dano autoritativo; popups e hit flash agora podem ser exibidos para todos os observadores.
- `TrapLogicBase` ganhou `InitializeServer(...)` e `NetworkedTrapVisual` passou a concentrar a ativacao visual; `BuildManager` inicializa estado antes de `NetworkObject.Spawn()`.
- `Espinhos.cs` agora usa o caminho autoritativo de dano com feedback sincronizado; `Teleportador.cs` delega o deslocamento para `PlayerTeleportService`.
- `DragonDefensiveStanceController` concentra a postura defensiva e o counter do Dragao; `PlayerHealthSystem` consulta interceptores de dano em vez de depender de um flag global.
- `TemorSismico.prefab` agora e um prefab de rede real; `TemorSismicoLogic` aplica knock-up de 2s via `EnemyStatusController`.
- `FumacaTinta.prefab`, `BombaSprayProjectile` e `NuvemDeTintaLogic` passaram a spawnar a nuvem do Polvo via servidor; o prefab foi registrado em `DefaultNetworkPrefabs.asset`.
- `EnemyController` ficou focado em alvo e chase; `EnemyCombatSystem` virou a maquina autoritativa de ataque para destravar a transicao `Chase -> Attack`.
- Validacao local: `dotnet build PI3D.sln` compilou com sucesso. Fora do Unity pode ser necessario regenerar os `.csproj` antes do build, porque essas inclusoes sao mantidas pelo editor.

## Atualizacao anterior: correcoes Host/Client Dragao e Polvo (2026-04-27)

- `NetworkGameplayResolver.cs` foi adicionado para centralizar resolucao de `CharacterIndex`, atacante e `PlayerHealthSystem`.
- `GameSetupManager` agora define `NetworkedPlayerController.CharacterIndex` no spawn e injeta `characterData` nos sistemas do player.
- `NetworkedPlayerController.CharacterIndex` ficou com escrita do servidor, e o registro de jogadores passou a ficar concentrado no `PlayerRegistry` via fluxo de setup.
- `CommanderAbilityController` saiu do polling via `Input.GetKeyDown` e agora usa `LocalPlayerInputBridge` com `Ability1`, `Ability2` e `Ultimate` (`Q`, `E`, `X`).
- O dano autoritativo de Dragao, Polvo, torres e tiros passou a carregar `attackerClientId` e `PlayerHealthSystem`, garantindo popup, hit flash e `TriggerDamageDealt` para o cliente correto.
- `MergulhoTintaLogic` passou a decidir entrada e saida no servidor, chamar `EnemyController.LoseTarget()` e reenviar a posicao final de superficie para o owner.
- `EnemyPoolManager` deixou de reparentear `NetworkObject` no caminho do cliente, removendo o `NotServerException`.
- `BuildManager` sanitiza ghosts e buildables runtime; torres nao herdam input, camera, audio ou scripts de player. `TorretaPolvo.prefab` tambem foi limpo.
- `CameraController` e `TopDownCameraManager` ficaram restritos a camera local ativa e a um unico conjunto de listeners (`AudioListener` e `StudioListener`).
- `PassiveEscamasAdamantium` e `PassivaTracoUrbano` agora respeitam autoridade de servidor e owner local.
- Validacao executada: `dotnet build PI3D.sln` concluiu com sucesso sem erros.

## O que mudou em relacao aos docs antigos

- O wrapper atual do EOS e `EOSManagerWrapper.cs`, nao `EOSManager.cs`.
- `NetworkedCurrency.cs` e `NetworkedHorde.cs` nao sao referencia ativa.
- O lobby publica `SERVER_ADDRESS`, `SERVER_PORT`, `RELAY_CODE` e `LOBBY_STATE`.
- A escolha de personagem viaja em `ConnectionApproval` e fica cacheada em `CharacterChoiceCache`.
- O fluxo atual separa melhor Editor/MPPM de builds.
- `PlayerNetworkSetup` centraliza o setup do jogador local e remoto.
- `PlayerIdentityBridge` liga `clientId` ao `productUserId` e ao `sessionToken`.
- `ServerAuthoritativeProjectile` faz dano no servidor, mas nao e um `NetworkObject` visual.

## Visao geral atual

```text
Login EOS -> criar/entrar lobby -> ready + personagem -> StartMatch
-> host publica dados de conexao -> clientes leem atributos do lobby
-> NGO conecta -> SceneMapTest carrega -> spawn dos players e inicio da partida
```

## Core de inicializacao

- `NetworkBootstrap.cs` segue como ponto de entrada para testes Host/Client com NGO.
- `EOSManagerWrapper.cs` carrega `EOSConfig_Main`, valida credenciais, aguarda o `EOSManager` externo e expoe `PlatformInterface`, `ConnectInterface` e `AuthInterface`.
- `EOSConfig.cs` carrega e valida `EOSCredentials.json`.
- `HostManager.cs` continua como helper para fluxos antigos de Host/Client.
- `UGSBootstrap.cs` inicializa Unity Services e auth anonima antes do Relay em builds.
- `WindowsPlatformSpecifics.cs` registra a implementacao Windows e isola cache/temp do EOS por clone MPPM.
- `MppmHelper.cs` detecta clones por `--virtual-project-clone`, `-vpId=` ou variavel de ambiente.

## Auth e sessao

- `EOSAuthenticator.cs` faz login anonimo via Device ID.
- Em clone MPPM ele remove o Device ID antigo antes de criar um novo.
- O `DeviceModel` recebe um sufixo de clone no MPPM.
- `SessionManager.cs` guarda `userId`, `displayName`, `currentLobbyId`, `currentMatchId` e `sessionToken`.
- Depois do login bem-sucedido, `EOSManagerWrapper.SetConnected(true)` e `SessionManager.StartSession()` sao chamados.

## Lobby e inicio de partida

- `LobbyData.cs` define `LobbyInfo`, `LobbyMember`, `LobbySettings`, `LobbySearchFilter`, `LobbyState`, `LobbyAttributes` e `MemberAttributes`.
- `LobbyManager.cs` faz create, search, join e leave, atualiza atributos de membro e publica dados de conexao da partida.
- `SetReady()` e `SelectCharacter()` atualizam os atributos do membro no EOS.
- `StartMatch()` cacheia a escolha do host, ativa `ConnectionApproval`, inicia o Host NGO e publica `RELAY_CODE`, `SERVER_ADDRESS` e `SERVER_PORT` quando o suporte de build esta pronto.
- Em Editor/MPPM o fluxo usa conexao direta com `127.0.0.1` ou IP local.
- `ProcessLobbyAttributes()` observa mudancas no lobby e dispara a conexao do cliente.
- `LeaveLobby()` limpa estado interno, sessao de lobby e cache de escolha de personagem.

## Identidade e spawn

- `CharacterChoiceCache.cs` guarda a escolha do host e dos clientes ate o spawn.
- `PlayerNetworkSetup.cs` resolve referencias do prefab do jogador, habilita o owner e desabilita componentes remotos.
- `PlayerIdentityBridge.cs` faz a ponte entre `clientId` do NGO, identidade EOS e token de sessao.
- `PlayerRegistry.cs` mantem o registro server-side dos jogadores conectados e ajuda a buscar o jogador mais proximo.

## Sync

- `NetworkedPlayerController.cs` continua como componente de sincronizacao.
- `NetworkedBuilding.cs` e o sync das construcoes.
- `NetworkedEnemy.cs` e o sync dos inimigos.
- `ServerAuthoritativeProjectile.cs` e um `MonoBehaviour` local que resolve dano no servidor e suprime a apresentacao visual.

## Game server e estado da partida

- `GameServerManager.cs` valida conexoes, registra jogadores e notifica entradas e saidas.
- `MatchManager.cs` controla `CurrentMatchState`, `CurrentWave` e `MatchTime` via `NetworkVariables`.

## UIs e testes atuais

- `LobbySceneUI.cs` e a interface canonica de lobby em Canvas.
- `LobbyUIManager.cs` e `LobbyPlaceholderUI.cs` continuam como superficies de teste e debug.
- `MenuLobbyPanel.cs` serve como painel simples para testes de menu.
- `EOSAuthTest.unity` valida apenas autenticacao.
- `Network Test.unity` valida Host/Client direto sem EOS Lobby.
- `LobbyScene.unity` valida auth + lobby + inicio de partida.
- `SceneMapTest.unity` valida o mapa de jogo apos a conexao.

## Arquivos atuais

- `Core/NetworkBootstrap.cs`
- `Core/EOSManagerWrapper.cs`
- `Core/EOSConfig.cs`
- `Core/HostManager.cs`
- `Core/MppmHelper.cs`
- `Core/WindowsPlatformSpecifics.cs`
- `Core/CharacterChoiceCache.cs`
- `Core/PlayerIdentityBridge.cs`
- `Core/UGSBootstrap.cs`
- `Auth/EOSAuthenticator.cs`
- `Auth/SessionManager.cs`
- `Lobby/LobbyData.cs`
- `Lobby/LobbyManager.cs`
- `Lobby/LobbySceneUI.cs`
- `Lobby/LobbyUIManager.cs`
- `GameServer/GameServerManager.cs`
- `GameServer/MatchManager.cs`
- `GameServer/PlayerRegistry.cs`
- `Sync/PlayerNetworkSetup.cs`
- `Sync/ServerAuthoritativeProjectile.cs`
- `Testing/EOSAuthTest.cs`
- `Testing/NetworkConnectionTest.cs`
- `Testing/LobbyPlaceholderUI.cs`
- `Testing/MenuLobbyPanel.cs`

## Nao usar como atual

- `EOSManager.cs` como arquivo do projeto.
- `NetworkedCurrency.cs` e `NetworkedHorde.cs` como verdade atual do fluxo multiplayer.
- `NetworkBootstrap.unity`, porque nao existe no repositorio atual.

## Resumo curto

O multiplayer atual e um fluxo EOS + Lobby + NGO com suporte a MPPM, Relay em builds,
identidade de jogador separada do `clientId` e setup local/remoto centralizado em
`PlayerNetworkSetup`.
