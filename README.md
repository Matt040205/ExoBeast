# ExoBeasts V3

Projeto Unity 6 (`6000.0.52f1`) de tower defense cooperativo para 1 a 4 jogadores. Jogadores escolhem comandantes, constroem torres e armadilhas, protegem a base e enfrentam ondas de inimigos em fluxo singleplayer ou multiplayer host/client.

## Estado Atual

- Estrutura ativa principal: `Assets/aaPasta`.
- Cenas ativas: `MenuScene`, `LobbyScene`, `EscolherPersonagem`, `CenaMapaNOVO`, `Win`, `Lose`, `Rastros`.
- Multiplayer: Netcode for GameObjects, Unity Transport, Epic Online Services e MPPM para teste multi-instancia.
- Audio: FMOD instalado em `Assets/Plugins/FMOD`; BetterFMOD embutido em `Packages/com.bisc8.betterfmod` e sempre usado por meio da camada `ExoAudioService`.
- Documentacao ativa: comece por [docs/INDEX.md](docs/INDEX.md).

## Regras De Organizacao

- Nao use documentos antigos como fonte operacional. Tudo que foi substituido esta em `docs/archive/`.
- Nao crie novas pastas paralelas fora de `Assets/aaPasta` para gameplay, personagens, mapas ou sistemas.
- Nao chame `FMODUnity.RuntimeManager` direto em gameplay; use `ExoAudioService`.
- Nao instale outro FMOD pelo instalador do BetterFMOD. A instalacao canonica e `Assets/Plugins/FMOD`.
- Qualquer personagem, mapa, som ou plugin novo precisa atualizar a documentacao e passar pelo checklist correspondente em `docs/checklists/`.

## Setup Rapido

1. Abra este diretorio no Unity Hub com Unity `6000.0.52f1`.
2. Configure as credenciais EOS seguindo [Assets/aaPasta/Multiplayer/CREDENTIALS_SETUP.md](Assets/aaPasta/Multiplayer/CREDENTIALS_SETUP.md).
3. Abra `Assets/aaPasta/Cenas/MenuScene.unity`.
4. Para multiplayer local, use Unity Multiplayer Play Mode com 2 instancias.

## Leitura Obrigatoria

| Tema | Documento |
| --- | --- |
| Indice operacional | [docs/INDEX.md](docs/INDEX.md) |
| Multiplayer | [docs/multiplayer.md](docs/multiplayer.md) |
| Audio e BetterFMOD | [docs/audio/README.md](docs/audio/README.md) |
| Personagens | [docs/personagens/README.md](docs/personagens/README.md) |
| Mapas | [docs/mapas/README.md](docs/mapas/README.md) |
| Plugins UPM | [docs/plugins/adicionar-plugin-upm.md](docs/plugins/adicionar-plugin-upm.md) |
