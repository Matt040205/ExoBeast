# Codex

Status: ativo
Publico: Codex
Ler primeiro: `Assets/Diretrizes_Multiagente.md`
Quando for multiplayer: `Assets/CoreScripts/Docs/Estado_Atual_Multiplayer.md`
Nao usar como fonte de verdade: docs historicas e nomes nao confirmados

Use este arquivo junto com `Assets/Diretrizes_Multiagente.md`.

## Memoria recente (2026-06-26)

- O projeto foi reorganizado: a estrutura fisica atual de `Assets` e a fonte de verdade.
- Cenas canonicas ficam em `Assets/Cenas/`; a cena tecnica de abertura e `Assets/Cenas/NetworkBootstrap.unity`, que carrega `MenuScene`.
- Scripts centrais de gameplay ficam em `Assets/CoreScripts/`; a camada multiplayer fica em `Assets/Multiplayer/`.
- A fonte canonica do multiplayer fica em `Assets/CoreScripts/Docs/Estado_Atual_Multiplayer.md`.
- Se for mexer em dano, teleporte, poca de tinta, knock-up, counter, espinhos ou HUD da Base, leia tambem os contratos em `Assets/CoreScripts/Combat/`.
