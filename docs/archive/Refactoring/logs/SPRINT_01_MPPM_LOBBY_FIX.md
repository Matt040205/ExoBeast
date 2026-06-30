# Sprint 1 - Correcao MPPM e LobbyScene

Data: 2026-05-21

## Status

Validado manualmente: o bloqueio que impedia testar Sprint 1 no Multiplayer Play Mode foi resolvido. O usuario confirmou que o Player 2 voltou a entrar na `LobbyScene`.

## Bloqueio MPPM

### Sintoma

No Player 2 do MPPM, ao clicar em Multiplayer, a transicao para `LobbyScene` falhava com:

```text
Scene 'LobbyScene' couldn't be loaded because it has not been added to the active build profile or shared scene list or the AssetBundle has not been loaded.
```

### Causa

O projeto principal enxergava `Assets/Scenes/LobbyScene.unity` no Build Settings, mas o clone MPPM tinha um Build Profile local com lista de cenas vazia em `Library/VP/.../Library/BuildProfiles/SharedProfile.asset`.

Esse cache local fazia `SceneUtility.GetBuildIndexByScenePath("Assets/Scenes/LobbyScene.unity")` retornar `-1` dentro do clone.

### Correcao aplicada

- Criado `Assets/Editor/BuildSceneListGuard.cs`.
- O guard valida e repara `EditorBuildSettings.globalScenes` e `EditorBuildSettings.scenes` antes do Play Mode.
- Menu manual disponivel em `Tools/ExoBeasts/Repair Build Scene List`.
- Lista canonica atual, nesta ordem:
  - `Assets/Scenes/MenuScene.unity`
  - `Assets/Scenes/EscolherPersonagem.unity`
  - `Assets/Scenes/LobbyScene.unity`
  - `Assets/Scenes/Rastros.unity`
  - `Assets/Scenes/CenaMapaTeste.unity`
  - `Assets/Scenes/Lose.unity`
  - `Assets/Scenes/Win.unity`
  - `Assets/Scenes/CenaMapaNOVO.unity`
- `Assets/Codigo/Managers/GameModeManager.cs` agora resolve a cena por path completo antes de carregar.
- Em Editor Play Mode, quando o build index nao existe por causa do clone MPPM, usa `EditorSceneManager.LoadSceneInPlayMode(scenePath, LoadSceneMode.Single)` como fallback editor-only.
- O log agora imprime a lista de build scenes visivel pelo processo quando a resolucao falha.

### Testes adicionados

Em `Assets/Tests/Editor/MenuSceneValidationTests.cs`:

- `CanonicalScenesAreEnabledAndOrderedInBuildSettings`
- `LobbySceneResolvesToBuildIndex`

Ultima execucao registrada depois do ajuste da LobbyScene: EditMode `10/10 Passed`.

## Ajuste LobbyScene

### Sintoma

Na `LobbyScene`, o botao `LobbyPublico` ficava pre-selecionado quando o mouse passava perto da area de "Entrar em Lobby por ID". O clique em `EntrarLobby` podia ser capturado pelo botao publico.

### Causa

Textos grandes eram filhos do botao `LobbyPublico` e ainda tinham `raycastTarget = true`. No EventSystem do Unity, um `Graphic` filho pode receber o raycast e o evento sobe para o `Button` pai, expandindo a area clicavel alem do retangulo visual do botao.

### Correcao aplicada

- Em `Assets/Scenes/LobbyScene.unity`, textos filhos de `EntrarLobby` e `LobbyPublico` tiveram `m_RaycastTarget: 0`.
- Em `Assets/Codigo/Multiplayer/Lobby/LobbySceneUI.cs`, `DisableButtonLabelRaycasts()` desliga raycast de `TMP_Text` dentro de botoes no `Awake`.
- O fallback de auto-deteccao do campo de ID agora tambem aceita o nome atual da cena, `PrcurarLobbyID`, alem do nome corrigido `ProcurarLobbyID`.

### Teste adicionado

Em `Assets/Tests/Editor/MenuSceneValidationTests.cs`:

- `LobbySceneJoinButtonsDoNotLetChildTextStealRaycasts`

Esse teste abre a `LobbyScene` e garante que textos filhos dos botoes `EntrarLobby` e `LobbyPublico` nao recebem raycast.

## Notas para o proximo agente

- Nao versionar nem depender de `Library/BuildProfiles` ou `Library/VP`; sao caches locais.
- Se o MPPM voltar a falhar depois de recompilacao ou troca de perfil, regenere/reabra o Player 2 para descartar o clone com Build Profile antigo.
- O warning do FMOD sobre porta ocupada no MPPM apareceu durante a validacao, mas nao bloqueou a correcao de cena.
- Antes de seguir para Sprint 2, rode novamente os EditMode tests e uma validacao manual MPPM: Main Editor + Player 2, clicar Multiplayer nos dois e confirmar chegada na `LobbyScene`.
