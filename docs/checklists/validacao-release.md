# Checklist: Validacao De Release

1. Unity abre sem erros de compilacao.
2. `Packages/manifest.json` e `packages-lock.json` estao consistentes.
3. Build Settings contem apenas cenas ativas.
4. Busca por caminhos e cenas legadas aparece apenas em `docs/archive`.
5. Busca por `RuntimeManager` em gameplay aparece apenas em `ExoAudioService`.
6. Singleplayer passa de `MenuScene` ate `CenaMapaNOVO`.
7. MPPM com 2 instancias passa por lobby, selecao e gameplay.
8. Audio de tiro, passos, habilidade, construcao, base, vitoria e derrota toca corretamente.
9. Nenhuma credencial real EOS aparece em Git.
