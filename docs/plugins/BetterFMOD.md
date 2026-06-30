# BetterFMOD

Fonte: https://github.com/Bisc8Studio/com.bisc8.betterfmod

## Como O Plugin Funciona

- Pacote UPM: `com.bisc8.betterfmod`.
- Versao declarada: `1.1.0`.
- Origem: commit `967e1f77055f3de83643fa06d32d857b26e33cce`.
- Forma atual no projeto: pacote embutido em `Packages/com.bisc8.betterfmod`.
- Define necessario: `FMOD_PRESENT`.
- Runtime principal: `FmodCommands`.
- Catalogo de eventos: assets `CreateFmodList`.

`FmodCommands` guarda eventos em dicionarios por string. Isso e pratico para one-shots, mas perigoso para loops por entidade quando varios jogadores usam o mesmo id. Por isso o projeto nao chama o plugin direto em gameplay; usa `ExoAudioService`.

O pacote foi embutido porque o instalador original tenta mover/copiar FMOD para `Assets/BISC8/BetterFMOD/FMOD`. Esse fluxo duplicou `FMODUnity` com `Assets/Plugins/FMOD` e gerou erro de assemblies duplicados. A versao embutida reconhece `Assets/Plugins/FMOD` como instalacao canonica e adiciona suporte a:

- one-shot 3D por posicao;
- loops por chave de instancia;
- pause/resume de loop por handle;
- volume de bus via BetterFMOD;
- `StopAllAudio`.

## Instalacao No Projeto

- `Packages/manifest.json` aponta para `file:com.bisc8.betterfmod`.
- O FMOD existente em `Assets/Plugins/FMOD` continua sendo a instalacao canonica.
- Nao executar a opcao de mover/copiar FMOD do instalador do plugin.
- Se Unity recusar o pacote por requisito `unity: 6000.3`, parar e tratar como bloqueio de compatibilidade.

## Validacao

- Unity compila sem duplicar `FMODUnity`.
- `ExoAudioService` cria `FmodCommands` automaticamente antes da primeira cena.
- Eventos precisam existir em `Assets/aaPasta/CoreScripts/Audio/Resources/ExoFmodEvents.asset`.
- Loops por jogador usam `AudioLoopHandle` e chaves unicas no BetterFMOD.
