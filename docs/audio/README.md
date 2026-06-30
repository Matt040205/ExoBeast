# Audio E BetterFMOD

Status: ativo.

## Estado Atual

- FMOD canonico: `Assets/Plugins/FMOD`.
- Configuracao FMOD: `Assets/Plugins/FMOD/Resources/FMODStudioSettings.asset`.
- BetterFMOD: pacote embutido em `Packages/com.bisc8.betterfmod`, originado do commit `967e1f77055f3de83643fa06d32d857b26e33cce`.
- Camada obrigatoria do projeto: `Assets/aaPasta/CoreScripts/Audio/ExoAudioService.cs`.
- Catalogo BetterFMOD: `Assets/aaPasta/CoreScripts/Audio/Resources/ExoFmodEvents.asset`.

## Regra Principal

Gameplay nao chama `RuntimeManager`, `EventInstance` ou `Bus` diretamente. Use:

- `ExoAudioService.PlayOneShot(eventId)`
- `ExoAudioService.PlayOneShot3D(eventId, position)`
- `ExoAudioService.CreateLoop(eventId, target)`
- `ExoAudioService.StartLoop(ref handle)`
- `ExoAudioService.StopLoop(ref handle)`
- `ExoAudioService.SetBusVolume(busPath, value)`
- `ExoAudioService.StopAll()`

## BetterFMOD

O BetterFMOD funciona com `FmodCommands` e assets `CreateFmodList`. O projeto usa essa integracao por tras de `ExoAudioService`, que cria um `FmodCommands` automaticamente antes da primeira cena e carrega listas em `Resources`.

Nao aceite nem recrie o fluxo `Move FMOD`. Ele criou uma segunda copia de FMOD em `Assets/BISC8/BetterFMOD/FMOD`, duplicou assemblies `FMODUnity` e quebrou a compilacao. O pacote embutido foi ajustado para reconhecer `Assets/Plugins/FMOD` como instalacao canonica.

## Eventos Documentados No Codigo

| Categoria | Eventos conhecidos |
| --- | --- |
| Musica | `event:/MUSIC/Musica_principal`, `event:/MUSIC/Victory_1`, `event:/MUSIC/Victory_2`, `event:/MUSIC/Defeat_1`, `event:/MUSIC/Defeat_2`, `event:/MUSIC/Defeat_3` |
| Player | `event:/SFX/Player/Footsteps/Dirt`, `event:/SFX/Player/Footsteps/Concrete`, `event:/SFX/Player/Footsteps/Water`, `event:/SFX/Player/Bow_Shot`, `event:/SFX/Player/Bow_Draw`, `event:/SFX/Player/Dash`, `event:/SFX/Player/Heal` |
| SFX | `event:/SFX/Atirar`, `event:/SFX/Atirar_segurando`, `event:/SFX/Recarga Arma`, `event:/SFX/Vento`, `event:/SFX/Espada`, `event:/SFX/Espada_1`, `event:/SFX/Espada_2`, `event:/SFX/Katana Vento`, `event:/SFX/Monstro` |
| Mundo | `event:/SFX/Base/Hit_Light`, `event:/SFX/Base/Hit_Heavy`, `event:/SFX/Enemies/Spider_Attack`, `event:/SFX/Enemies/Scorpion_Attack`, `event:/SFX/Enemies/Monster_Growl`, `event:/SFX/Enemies/Eagle_Screech`, `event:/SFX/Towers/Shot_Magic`, `event:/SFX/Towers/Spawn_Magic` |

## Pendencias Para Fechar 9+

- Dividir `ExoFmodEvents.asset` por categoria somente se o catalogo crescer ou ficar dificil de revisar.
- Validar qual pasta de bancos e carregada em runtime e remover copias duplicadas somente depois do teste.
- Documentar o processo de build dos bancos FMOD.
- Registrar dono, banco e cena de teste de cada evento.
