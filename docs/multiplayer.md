# Multiplayer

Status: ativo. Este arquivo substitui a visao antiga baseada na estrutura anterior do projeto.

## Fonte Ativa

- Codigo: `Assets/aaPasta/Multiplayer`.
- Cenas: `Assets/aaPasta/Cenas/MenuScene.unity`, `LobbyScene.unity`, `EscolherPersonagem.unity`, `CenaMapaNOVO.unity`.
- Credenciais: `Assets/aaPasta/Multiplayer/CREDENTIALS_SETUP.md`.
- Autenticacao EOS: `Assets/aaPasta/Multiplayer/Docs/AUTHENTICATION_GUIDE.md`.

## Fluxo De Jogo

1. `MenuScene`: entrada e escolha de fluxo.
2. `LobbyScene`: autenticacao EOS, criar/buscar/entrar em sala.
3. `EscolherPersonagem`: selecao de comandante e composicao de equipe.
4. `CenaMapaNOVO`: mapa ativo de gameplay.
5. `Win` ou `Lose`: cenas finais com audio sincronizado via RPC.

## Subsystems

| Area | Scripts principais |
| --- | --- |
| Auth | `EOSAuthenticator`, `SessionManager` |
| Core | `NetworkBootstrap`, `EOSManagerWrapper`, `PlayerIdentityBridge`, `MultiplayerRuntimeReset`, `MppmHelper` |
| Lobby | `LobbyManager`, `LobbySceneUI`, `LobbyButtonBinder`, `LobbyMembershipService`, `LobbyNotificationDispatcher` |
| GameServer | `MatchManager`, `MatchSessionLauncher`, `PlayerRegistry` |
| Sync | `NetworkedPlayerController`, `NetworkedEnemy`, `NetworkedBuilding`, `NetworkedTrapVisual`, `ServerAuthoritativeProjectile` |
| Testing | `EOSAuthTest`, `NetworkConnectionTest`, `NetworkedCubeMovement` |

## Regras Que Nao Podem Regredir

- Managers legados de host/server nao sao fontes ativas. Referencias a eles pertencem apenas ao arquivo historico em `docs/archive/`.
- Toda transicao de cena multiplayer deve ser iniciada pelo servidor.
- Prefabs spawnaveis precisam estar em `DefaultNetworkPrefabs`.
- MPPM precisa continuar funcionando com host e cliente no mesmo PC.
- Credenciais reais EOS nunca entram no Git.

## Smoke Test Obrigatorio

- Abrir `MenuScene`.
- Instancia host cria lobby.
- Instancia cliente entra.
- Host inicia partida.
- Ambos passam por `EscolherPersonagem` e chegam em `CenaMapaNOVO`.
- Validar movimento, tiro, construcao, inimigos, dano na base, vitoria/derrota e audio sem duplicacao.
