# Checklist: Adicionar Audio

1. Criar evento no projeto FMOD.
2. Gerar bancos.
3. Confirmar que o banco canonico e carregado pelo Unity.
4. Registrar o evento em `docs/audio/README.md`.
5. Adicionar ao catalogo BetterFMOD em `Assets/aaPasta/CoreScripts/Audio/Resources/ExoFmodEvents.asset`.
6. Chamar audio em gameplay somente por `ExoAudioService`.
7. Para loops, usar `AudioLoopHandle`.
8. Testar host e cliente para evitar duplicacao.
9. Testar troca de cena para confirmar que loops param.
