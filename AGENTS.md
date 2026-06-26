# AGENTS

Status: ativo
Publico: agentes Codex, Claude e Gemini

Leia primeiro:

- `Assets/Diretrizes_Multiagente.md`
- `Assets/CoreScripts/Docs/Estado_Atual_Multiplayer.md` quando tocar multiplayer

Regras de manutencao:

- Use a estrutura fisica atual como verdade: `Assets/CoreScripts`, `Assets/Cenas`, `Assets/Multiplayer`, `Assets/Configurações`, `Assets/Endereçáveis` e pastas equivalentes.
- Nao recrie caminhos antigos como `Assets/Codigo`, `Assets/Scenes` ou `Assets/aaPasta`.
- Preserve contratos publicos de `LobbyManager`, `EOSAuthenticator`, `SessionManager`, `NetworkBootstrap`, `PlayerIdentityBridge`, `Networked*` e `PlayerNetworkSetup`.
- Segredos EOS continuam fora do git: nao versionar `EOSCredentials.json`, configs gerados de EOS ou `.env`.
- Docs antigas podem existir como historico, mas codigo atual e docs ativos vencem nomes antigos.
