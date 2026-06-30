# Indice Operacional

Este e o mapa de documentacao ativa do projeto. Se um documento contradiz este indice, ele esta errado ou deve ir para `docs/archive/`.

## Fontes Ativas

| Tema | Documento |
| --- | --- |
| Multiplayer | [multiplayer.md](multiplayer.md) |
| Audio e BetterFMOD | [audio/README.md](audio/README.md) |
| Personagens | [personagens/README.md](personagens/README.md) |
| Mapas | [mapas/README.md](mapas/README.md) |
| Checklist: personagem | [checklists/adicionar-personagem.md](checklists/adicionar-personagem.md) |
| Checklist: mapa | [checklists/adicionar-mapa.md](checklists/adicionar-mapa.md) |
| Checklist: audio | [checklists/adicionar-audio.md](checklists/adicionar-audio.md) |
| Checklist: validacao | [checklists/validacao-release.md](checklists/validacao-release.md) |
| Plugins UPM | [plugins/adicionar-plugin-upm.md](plugins/adicionar-plugin-upm.md) |
| BetterFMOD | [plugins/BetterFMOD.md](plugins/BetterFMOD.md) |

## Estado Organizacional

- Nota inicial auditada: 5,6/10.
- Meta: 9+ com docs vivas, audio centralizado, cenas corretas, pastas previsiveis e validacao repetivel.
- Pasta de gameplay ativa: `Assets/aaPasta`.
- Docs historicas: `docs/archive`.

## Regras De Manutencao

- Atualize docs no mesmo PR que altera personagem, mapa, audio, multiplayer ou plugin.
- Nao crie guia novo quando um guia ativo existente deve ser atualizado.
- Use nomes de dominio em portugues para docs e taxonomia de assets.
- Preserve GUIDs Unity ao mover assets.
- Scripts de gameplay nao devem depender diretamente de plugin novo sem uma camada propria do projeto.
