# Ponte / CenaMapaNOVO

Status: ativo, documentacao inicial.

## Estrutura

- Cena canonica: `Assets/aaPasta/Cenas/CenaMapaNOVO.unity`.
- NavMesh: `Assets/aaPasta/Cenas/CenaMapaNOVO/NavMesh-NavMeshSurface.asset` e `NavMesh-NavMeshSurface 1.asset`.
- Assets de mapa: `Assets/aaPasta/Mapas/Ponte`.
- Prefab raiz encontrado: `Assets/aaPasta/Mapas/Ponte/LiteralmenteMapa/Mapa.prefab`.

## Validacao Obrigatoria

- Cena esta no Build Settings sem entrada antiga de cena legada.
- Host e cliente entram na cena a partir de `EscolherPersonagem`.
- Players spawnam em posicoes validas.
- Inimigos percorrem NavMesh ate a base.
- Torres e armadilhas podem ser posicionadas apenas em locais validos.
- Audio de tiro, construcao, inimigo e base toca uma vez por evento.

## Pendencias

- Nomear e documentar todos os spawn points.
- Documentar rotas de inimigos e limites jogaveis.
- Documentar configuracao de iluminacao.
- Definir se `Assets/aaPasta/Mapas/Futuro` e backlog, prototipo ou mapa em producao.
