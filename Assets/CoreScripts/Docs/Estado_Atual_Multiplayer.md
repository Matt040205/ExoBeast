# Estado Atual do Multiplayer

Status: canonico
Publico: agentes e devs do multiplayer
Ler primeiro: `Assets/Diretrizes_Multiagente.md`
Nao usar como fonte de verdade: docs historicas, planos antigos e nomes removidos

Documento canonico do multiplayer atual. Leia este arquivo para entender o que existe hoje,
o que mudou em relacao aos docs antigos e quais nomes devem ser tratados como atuais.

## Ultima atualizacao: CenaSelecao canonica no fluxo multiplayer (2026-08-07)

- `CenaSeleçao` substitui `EscolherPersonagem` como cena canonica de selecao para singleplayer e multiplayer.
- `EscolherPersonagem.unity` fica como asset legado/historico e nao deve ser usada em novos fluxos, build settings canonicos ou testes de validacao.
- `SelecaoEquipeFlowManager` e a interface ativa da selecao nova; ele preserva o contrato multiplayer de comandante autoritativo via `CharacterChoiceCache`, `LobbyManager.SelectCharacter`, `PartySlotLayout` e ready por membro do lobby.
- No multiplayer, cada jogador continua limitado aos slots de `PartySlotLayout`: primeiro slot local e comandante; slots restantes sao torres daquele jogador.
- `AbaDeOutrosJogadores` dentro da `CenaSeleçao` e o painel oficial para status multiplayer, lista de membros e botao de pronto durante a selecao.
- O host so inicia `CenaMapaNOVO` pela selecao quando todos os membros estao prontos e todos os clients conectados possuem escolha autoritativa em `CharacterChoiceCache`.

## Ultima atualizacao: estabilizacao Menu/Lobby/Selecao (2026-07-25)

- `MenuManager` rebinda os botoes principais por nome sem depender de maiusculas/minusculas e usa os IDs atuais do `MenuTabSlider` (`Options` e `Credits`).
- `LobbySceneUI` aceita aliases reais da cena nova, incluindo `IniciarPartida`, campo `ID`, painel publico com variacoes de nome e botao `EntrarLobbyTransferencia`.
- O botao de iniciar no lobby fica visivel apenas para o host e interagivel apenas quando todos os membros do lobby estao prontos.
- Ao entrar em `CenaSeleçao` no multiplayer, o ready herdado do lobby e resetado para `false`; o jogador precisa escolher comandante e primeira torre antes de poder marcar pronto para a partida.
- `SelecaoManager.MostrarCaminhoDeUpgrade(int)` foi restaurado como API publica de compatibilidade para listeners antigos da cena de selecao.
- Validacao historica MCP em Play Mode com 1 jogador usava `EscolherPersonagem`; para validacoes novas, usar `MenuScene` -> `LobbyScene` -> login EOS Device ID -> `LobbySceneUI.CriarSala()` -> ready -> host match -> `CenaSeleçao` -> selecao minima host -> `CenaMapaNOVO` via `NetworkManager.SceneManager`.
- Limitacao observada no MCP: `execute_code` falha neste projeto com `mono.exe: O nome do arquivo ou a extensao e muito grande`. Para o smoke test foi usado um harness temporario de Editor, removido ao final.

## Ultima atualizacao: reparo FMOD em maquina nova (2026-07-25)

- Sintoma: popup `Repair FMOD Libraries` bloqueava o Unity/MCP indicando line endings incorretos nos bundles macOS do FMOD.
- Causa confirmada: os arquivos `Contents/Info.plist` dentro de `Assets/Plugins/FMOD/platforms/mac/lib/*.bundle` estavam com CRLF. A regra antiga `*.bundle binary` nao protegia arquivos internos do diretorio `.bundle`.
- Correcao aplicada: os tres `Info.plist` foram normalizados para LF-only e `.gitattributes` passou a forcar `Assets/Plugins/FMOD/platforms/mac/lib/**/*.plist text eol=lf`.
- Arquivos FMOD reparados:
  - `Assets/Plugins/FMOD/platforms/mac/lib/fmodstudio.bundle/Contents/Info.plist`
  - `Assets/Plugins/FMOD/platforms/mac/lib/fmodstudioL.bundle/Contents/Info.plist`
  - `Assets/Plugins/FMOD/platforms/mac/lib/resonanceaudio.bundle/Contents/Info.plist`
- Validacao: cada `Info.plist` ficou com `CRLF=0`, `CR=0`, `LF=36`; o popup foi fechado com `Repair` e o Unity MCP voltou a responder `manage_scene/read_console`.

## Docs ativos relacionados

- `Assets/CoreScripts/Docs/ONBOARDING.md` - guia de primeiro acesso para devs novos
- `Assets/CoreScripts/Docs/PADROES_NGO.md` - padroes NGO especificos do projeto (bugs reais documentados)
- `Assets/CoreScripts/Docs/Guia_Game_Designer.md` - explicacao sistema a sistema
- `Assets/CoreScripts/Docs/Guia_Setup_Multiplayer_Cenas.md` - setup de cena e prefab
- `Assets/CoreScripts/Docs/GUIA_PERSONAGENS.md` - CharacterBase, componentes do prefab, habilidades Q/E/X, Rastros
- `Assets/CoreScripts/Docs/GUIA_TORRES_ARMADILHAS.md` - towerData, sistema de upgrade, TowerBehavior, TrapDataSO
- `Assets/CoreScripts/Docs/GUIA_INIMIGOS_E_ONDAS.md` - EnemyDataSO, WaveConfig, HordeManager inspector
- `Assets/Multiplayer/Docs/AUTHENTICATION_GUIDE.md` - login EOS
- `Assets/Multiplayer/CREDENTIALS_SETUP.md` - segredos EOS
- `Assets/Multiplayer/README.md` - indice curto

## Ultima atualizacao: Unity 6.3 LTS + reorganizacao de Assets (2026-06-26)

- Versao alvo do projeto: Unity `6000.3.10f1`.
- A estrutura fisica atual de `Assets` e canonica: `CoreScripts`, `Cenas`, `Multiplayer`, `Configurações`, `Documentação`, `Endereçáveis`, `VFXgenerico` e equivalentes.
- Cenas canonicas ficam em `Assets/Cenas/`; `Assets/Cenas/NetworkBootstrap.unity` e a cena tecnica de abertura e carrega `MenuScene`.
- A lista NGO canonica fica em `Assets/Multiplayer/Setup/DefaultNetworkPrefabs.asset`.
- O mapa jogavel atual e `CenaMapaNOVO`. O nome `CenaMapaTeste` aparece apenas em contexto historico ou em classes legadas de bootstrap direto.
- Pastas legadas de codigo, cenas e organizacao antiga nao devem ser recriadas.

## Ultima atualizacao: Sprint 8 — integracao final + doc sync (2026-05-22)

- Sincronizado o doc canonico com o estado real do codigo apos Sprints 4–7:
  - `LobbyPlaceholderUI.cs` e `MenuLobbyPanel.cs` removidos da lista de arquivos ativos (foram deletados no Sprint 6).
  - `LobbyUIManager.cs` requalificado como tombstone `#if UNITY_EDITOR [Obsolete]`, nao e UI de producao.
  - Adicionados a "Arquivos atuais": `LobbyNotificationDispatcher.cs` (S4), `MatchSessionLauncher.cs` (S3), `LobbyMembershipService.cs` (S5), `LobbyButtonBinder.cs` (S6), `NetworkAddressHelper.cs` (S6), `PartySlotLayout.cs` (S6), `EosLobbyModHelper.cs` (S7).
- Fluxo de integracao final confirmado funcional: `CriarSala(mapName="EscolherPersonagem")` → host clica "Iniciar Partida" (habilitado apenas quando `AllMembersReady()`) → `MatchSessionLauncher.LaunchHostCoroutine` publica `SERVER_ADDRESS`/`RELAY_CODE`/`LOBBY_STATE=InGame` → clientes detectam via `OnLobbyAttributeUpdated` → `ConnectAsClient*` → `WaitForAllClientsAndLoadScene("EscolherPersonagem")` → NGO carrega cena para todos.
- Ready chain remota confirmada: membro remoto clica Pronto → EOS propaga → `OnMemberAttributeChanged` → `member.isReady = true` (mutacao in-place) → `InvokeOnMemberUpdated` → `AtualizarBotaoIniciar` → `AllMembersReady()` retorna true → botao host fica interagivel.

## Ultima atualizacao anterior: Sprint 7 — ready flow + extraçao helpers EOS (2026-05-22)

- `LobbySceneUI` agora implementa o fluxo de ready state corretamente: botao "Pronto"/"BtnPronto"/"BtnReady"/"Ready" (qualquer um desses nomes no hierarquia) dispara `ToggleReady()`, que chama `_lobby.SetReady(!_isReady)` e atualiza o visual do botao (texto e cor).
- `_isReady` e resetado para `false` ao criar ou entrar em uma sala, garantindo que o player sempre comeca como nao-pronto em cada novo lobby.
- O botao "Iniciar Partida" do host so fica interagivel quando `AllMembersReady()` retornar `true` — agora isso e alcancavel.
- Extraido `Core/EosLobbyModHelper.cs`: helper interno com `AddStringAttr`, `AddInt64Attr` e `AddStringMemberAttr`. Eliminada a duplicacao que existia em `LobbyManager.cs` e `MatchSessionLauncher.cs` (cada um tinha copia privada identica dos mesmos metodos).
- `LobbyManager` e `MatchSessionLauncher` agora delegam para `EosLobbyModHelper`.
- Sprint 6 (2026-05-22): removidos `LobbyPlaceholderUI.cs` e `MenuLobbyPanel.cs` (UI OnGUI de teste substituida pelo `LobbySceneUI` Canvas); adicionados `LobbyButtonBinder`, `NetworkAddressHelper`, `PartySlotLayout`, `LobbyMembershipService.cs.meta`.

## Ultima atualizacao anterior: refactor do sistema de credenciais EOS (2026-05-13)

- A "gambiarra" do sistema de credenciais foi removida. Tres scripts redundantes (`EOSConfigSetup.cs`, `EOSConfigImporter.cs`, `EOSBuildProcessor.cs`) foram deletados e substituidos por um unico componente: `Assets/Editor/EOSConfigGenerator.cs`.
- Quatro arquivos com `ClientSecret` em texto plano foram removidos do indice git (`eos_product_config.json`, `eos_windows_config.json`, `EpicOnlineServicesConfig.json`, `EOSConfig_Main.asset`) e adicionados ao `.gitignore`. As copias locais foram preservadas para nao quebrar o desenvolvimento.
- Nova cadeia de prioridade para carregar credenciais: variaveis de ambiente (`EOS_*`) -> `EOSCredentials.json` na raiz -> `StreamingAssets/EOS/*.json` (fallback runtime).
- `EOSConfigGenerator` roda automaticamente: pre-build via `IPreprocessBuildWithReport` (callbackOrder = -100), play mode via `EditorApplication.playModeStateChanged`, e tem menu `Tools > ExoBeasts > Generate EOS Config`.
- `EOSConfig.cs` (ScriptableObject) marcou todos os campos de credencial com `[NonSerialized]`. Resultado: o `.asset` nao persiste mais secrets.
- `EOSManagerWrapper.cs` chama `LoadCredentials()` (renomeado de `LoadCredentialsFromFile`) e mascara `ClientId` nos logs; `ClientSecret` nunca aparece em log.
- Pos-refactor, o Unity MCP `read_console` confirmou compilacao sem erros relacionados a EOS. Validacao funcional (Play Mode e build standalone) ainda pendente.
- Documentacao atualizada: `Assets/Multiplayer/CREDENTIALS_SETUP.md` cobre as tres formas de fornecer credenciais (env vars, JSON, template) com exemplos de CI/CD.
- Template versionado criado em `EOSCredentials.json.template` na raiz do projeto.

## Atualizacao anterior: blindagem do fluxo de startup, lobby e play direto (2026-04-30)

- O fluxo de entrada foi endurecido para nao depender de estado residual entre singleplayer, multiplayer e Play direto.
- `MenuManager` agora rebinds os botoes `Singleplayer` e `Multiplayer` por nome se o Inspector perder as referencias, e sempre sobrescreve listeners antigos da cena.
- `GameModeManager` passou a executar as trocas de modo atraves de `MultiplayerRuntimeReset`, que limpa lobby, sessao, callbacks pendentes e estado de NGO antes de voltar para local ou multiplayer.
- `LobbyManager` foi protegido contra callbacks EOS residuais quando o jogo nao esta em fluxo multiplayer ativo, incluindo limpeza de `currentLobbyId` e `currentMatchId` e cancelamento de conexoes pendentes.
- `LobbyUIManager` ficou apenas como superficie legado/teste, sem auto-redirecionamento ou auto-inicio de fluxo.
- `LobbySceneUI` agora usa o reset compartilhado ao voltar para o menu, evitando reconexao involuntaria.
- `CenaMapaTeste` ganhou `CenaMapaTesteDirectPlayBootstrap`, que garante os singletons minimos, restaura a equipe do ultimo save e aplica fallback debug quando o save nao vier valido.
- `GameDataManager` ganhou suporte de bootstrap para reutilizar a biblioteca original de personagens sem depender da passagem previa por `EscolherPersonagem`.
- `MenuScene` e `EscolherPersonagem` tiveram listeners persistentes antigos limpos, e a cena duplicada historica de lobby foi arquivada como `LobbyScene_Legacy.unity` para evitar entrada na cena errada.
- Foram adicionados testes de validacao em `Assets/Tests/Editor/` para checar refs criticas da `MenuScene`, listeners proibidos e consistencia das cenas canonicas.
- Validacao: os scripts modificados passaram no `validate_script` do Unity MCP; o runner de testes ainda foi inconsistente em uma execucao, entao a confirmacao final continua sendo mais confiavel direto no Editor Unity.

## Atualizacao anterior: investigacao de armadilhas multiplayer e fallback visual da Polva (2026-04-29)

- Sintomas observados nesta rodada:
  - no host, o limite global das armadilhas nao era respeitado;
  - a UI de contagem de armadilhas nao refletia o estado real do mapa;
  - algumas armadilhas entravam no mapa mas nao funcionavam como esperado;
  - o `Player.log` mostrava praticamente nada do ciclo de vida das armadilhas, entao a investigacao dependia mais do codigo do que do log.
- A primeira leitura do `Player.log` confirmou que havia fluxo de host, lobby, spawn e horda, mas quase nenhum evento de spawn/registro/despawn das armadilhas.
- Tambem foi feita uma varredura nos prefabs de armadilha e apareceu um padrao suspeito: varios `NetworkObject` de armadilha visual estavam com `GlobalObjectIdHash: 0`, e alguns prefabs logicos importantes tambem estavam inconsistentes. Isso virou a principal suspeita para as armadilhas que "aparecem" mas nao se comportam direito em rede.

### Tentativas feitas nas armadilhas

| Tentativa | O que foi alterado | Resultado pratico |
|---|---|---|
| 1 | A contagem da UI de armadilhas foi unificada para host e clientes via `syncedTrapCounts` no `BuildManager`, com `BuildButtonUI` e `UIManager` recebendo refresh dedicado em vez de recriar a loja inteira. | Compilou e melhorou a base da UI, mas nao resolveu o host construir acima do limite nem tornou o contador confiavel no runtime da partida. |
| 2 | O `BuildManager` passou a inicializar snapshot de contagem no `OnNetworkSpawn`, atualizar o snapshot local antes de broadcast, limpar o snapshot no `OnNetworkDespawn` e reenviar as contagens para clientes tardios. | A arquitetura ficou mais consistente, mas o problema persistiu no jogo real. |
| 3 | O `NetworkedTrapVisual` foi modificado para se registrar e desregistrar de forma autoritativa no `BuildManager`, com reconciliacao quando o `TrapIndex` muda e com confirmacao explicita de registro logo apos `Spawn()`. | Compilou, adicionou logs e deixou o ciclo de vida mais defensivo, mas o comportamento da trap ainda nao ficou correto em runtime. |
| 4 | Foram adicionados logs de debug para spawn concluido, registro, remoção, falha de spawn e rejeicao por limite/custo/setup. | Isso melhora a proxima investigacao, mas nao corrige o bug por si so. |
| 5 | As cenas multiplayer tiveram o `UIManager` duplicado removido para evitar comportamento nao deterministico de UI. | A limpeza ajudou a reduzir ruido, mas nao eliminou o problema principal das armadilhas. |
| 6 | A analise dos prefabs revelou `GlobalObjectIdHash: 0` em todos os visuais de armadilha de `Assets/Mapa prefab` e tambem em alguns prefabs logicos de armadilhas. | Ficou como forte suspeita de problema de NGO/prefab/hash, mas nao houve correção definitiva nesta rodada. |

### Estado deixado por esta investigacao

- O limite global por tipo continua sendo a regra do projeto, mas a implementacao de armadilhas ainda nao foi fechada de forma confiavel.
- O host ainda precisava ser validado com uma nova rodada de teste depois das correcoes, porque as tentativas anteriores nao resolveram o problema de forma definitiva.
- A memoria do projeto agora considera a suspeita de hash/prefab e a ausencia de logs de ciclo de vida como pontos centrais para a proxima investigacao.

### Fallback visual da Polva

- A habilidade `HabilidadeMergulhoTinta` ganhou um fallback simples para a poa de tinta enquanto o shader definitivo nao estiver pronto.
- O `ScriptableObject` passou a expor `fallbackPuddleSprite` e `fallbackPuddleWorldSize` alem do `visualPuddlePrefab` ja existente.
- `MergulhoTintaLogic` agora tenta renderizar a poa com a `Sprite` simples antes de cair no prefab visual mais complexo.
- O asset atual da habilidade foi preenchido com `PocaTinta.png`, entao o fluxo de teste imediato passa a usar uma imagem tosca em vez de depender do VFX.
- Esse fallback resolve o problema pratico de visual temporario, mas nao substitui a versao final de arte nem muda a autoridade da habilidade no multiplayer.

## Atualizacao anterior: correcoes de input local, build toggle e disputa de PlayerInput (2026-04-29)

- O problema observado no log nao era mais de spawn, auth ou lobby: o host completava login EOS, criava lobby, entrava na partida e o `PlayerNetworkSetup` terminava o setup local, mas o comandante ainda nao respondia aos inputs de gameplay.
- A investigacao historica mostrou um `PlayerInput` de cena no objeto `ManagersDaPartida`, com o action `Player/Build` ligado diretamente a `BuildManager.OnBuild`. Esse componente competia com o `PlayerInput` do player local pelo mesmo teclado/mouse.
- O `PlayerInput` do player local tambem passava por um ciclo `disable -> enable` em `PlayerNetworkSetup`, o que podia deixar referencias de `InputAction` cacheadas em estado velho dentro do `LocalPlayerInputBridge`.
- `PauseControl` ja estava mapeado no prefab, mas nao possuia um callback `OnPause(InputAction.CallbackContext)` compativel com o Input System novo.

### Correcoes aplicadas

| Arquivo | O que foi ajustado |
|---|---|
| `Assets/CoreScripts/Towers/BuildManager.cs` | Desabilita o `PlayerInput` de cena em `Awake()` para evitar disputa de device com o comandante local. O toggle de build passou a ser lido via `LocalPlayerInputBridge` do owner, com fallback para `Keyboard.current.bKey.wasPressedThisFrame`. |
| `LocalPlayerInputBridge.cs` | Passou a recachear bindings quando o `PlayerInput` muda de estado, incluindo a action `Build`. Tambem ganhou `ConsumeBuildPressed()`, flags de estado para `Build` e refresh explicito apos reset do `PlayerInput`. |
| `Assets/Multiplayer/Sync/PlayerNetworkSetup.cs` | Depois do `disable -> enable -> SwitchCurrentActionMap("Player")`, agora chama `RefreshBindingsAfterPlayerInputReset()` no bridge. O sanity-check do owner tambem passou a verificar `devices.Count` e `currentActionMap.enabled`. |
| `Assets/CoreScripts/Managers/PauseControl.cs` | Ganhou `OnPause(InputAction.CallbackContext)` para receber o evento do action `Player/Pause` que ja estava ligado no prefab. |

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
- Foi criada a pasta `Assets/CoreScripts/Combat/` com `DamageContext`, `DamageFeedbackMode`, `DamageRequest`, `DamageResponse` e `IDamageInterceptor` para padronizar validacao, bloqueio e feedback de dano.
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

- `LobbySceneUI.cs` e a interface canonica de lobby em Canvas (Sprint 6+).
- `LobbyUIManager.cs` e um tombstone `#if UNITY_EDITOR` marcado `[Obsolete]` — nao e UI real, impede CS2001 em clones MPPM antigos.
- `LobbyPlaceholderUI.cs` e `MenuLobbyPanel.cs` foram DELETADOS no Sprint 6 (substituidos por `LobbySceneUI`).
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
- `Core/PartySlotLayout.cs`          ← Sprint 6: layout dinamico de slots por numero de jogadores
- `Core/NetworkAddressHelper.cs`     ← Sprint 6: deteccao de IP local isolada
- `Core/EosLobbyModHelper.cs`        ← Sprint 7: helpers EOS extraidos de LobbyManager e MatchSessionLauncher
- `Auth/EOSAuthenticator.cs`
- `Auth/SessionManager.cs`
- `Lobby/LobbyData.cs`
- `Lobby/LobbyManager.cs`
- `Lobby/LobbySceneUI.cs`
- `Lobby/LobbyUIManager.cs`          ← tombstone #if UNITY_EDITOR [Obsolete]; nao e UI real
- `Lobby/LobbyMembershipService.cs`  ← Sprint 5: gestao de membros extraida de LobbyManager
- `Lobby/LobbyNotificationDispatcher.cs` ← Sprint 4: notificacoes EOS extraidas de LobbyManager
- `Lobby/LobbyButtonBinder.cs`       ← Sprint 6: wiring de botoes por nome de GameObject
- `GameServer/MatchSessionLauncher.cs` ← Sprint 3: orquestracao de StartHost/StartClient
- `GameServer/GameServerManager.cs`
- `GameServer/MatchManager.cs`
- `GameServer/PlayerRegistry.cs`
- `Sync/PlayerNetworkSetup.cs`
- `Sync/ServerAuthoritativeProjectile.cs`
- `Testing/EOSAuthTest.cs`
- `Testing/NetworkConnectionTest.cs`

## Nao usar como atual

- `EOSManager.cs` como arquivo do projeto.
- `NetworkedCurrency.cs` e `NetworkedHorde.cs` como verdade atual do fluxo multiplayer.
- `NetworkBootstrap.unity`, porque nao existe no repositorio atual.

## Resumo curto

O multiplayer atual e um fluxo EOS + Lobby + NGO com suporte a MPPM, Relay em builds,
identidade de jogador separada do `clientId` e setup local/remoto centralizado em
`PlayerNetworkSetup`.
