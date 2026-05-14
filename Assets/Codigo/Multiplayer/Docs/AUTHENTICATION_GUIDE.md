# Guia de Autenticacao EOS

Status: ativo
Publico: quem altera login e sessao EOS
Ler primeiro: [../../Docs/Estado_Atual_Multiplayer.md](../../Docs/Estado_Atual_Multiplayer.md)
Nao usar como fonte de verdade: fluxo geral de lobby, spawn e gameplay

Guia focado apenas em login EOS e sessao. Nao repete a arquitetura geral do multiplayer.

## Escopo

- `Core/EOSManagerWrapper.cs`
- `Auth/EOSAuthenticator.cs`
- `Auth/SessionManager.cs`
- `Core/WindowsPlatformSpecifics.cs`
- `Core/MppmHelper.cs`

## Ordem de inicializacao

1. `WindowsPlatformSpecifics` registra a implementacao Windows antes de qualquer cena.
2. O plugin EOS externo inicializa (le configs de `StreamingAssets/EOS/`).
3. `EOSManagerWrapper` chama `eosConfig.LoadCredentials()` e aguarda o SDK ficar pronto.
4. `EOSAuthenticator.LoginWithDeviceId()` faz o login anonimo.

## Carga de credenciais (refactor 2026-05-13)

O `EOSConfig.LoadCredentials()` tenta tres fontes em ordem:

1. **Variaveis de ambiente** (prioridade): `EOS_PRODUCT_ID`, `EOS_SANDBOX_ID`, `EOS_DEPLOYMENT_ID`, `EOS_CLIENT_ID`, `EOS_CLIENT_SECRET`, `EOS_ENCRYPTION_KEY`, `EOS_ENVIRONMENT`.
2. **`EOSCredentials.json` na raiz** do projeto (gitignored). Em MPPM clones, o path e ajustado via `MppmHelper.IsClone`.
3. **`StreamingAssets/EOS/*.json`** (fallback runtime — os arquivos gerados pelo `EOSConfigGenerator`).

Se nenhuma fonte for encontrada, `LoadCredentials()` loga erro com instrucoes claras e o init falha em `EOSManagerWrapper` com `OnInitializationFailed`.

Os JSONs em `StreamingAssets/EOS/` sao gerados automaticamente pelo `Assets/Editor/EOSConfigGenerator.cs`:
- Antes de cada build via `IPreprocessBuildWithReport.OnPreprocessBuild`
- Ao entrar em Play Mode via `EditorApplication.playModeStateChanged` (estado `ExitingEditMode`)
- Manualmente via menu `Tools > ExoBeasts > Generate EOS Config`

## Fluxo atual de login

- Se o jogo esta em clone MPPM, o autenticador remove o Device ID antigo antes de criar um novo.
- O `DeviceModel` recebe um sufixo de clone no MPPM para evitar colisao entre instancias.
- `DuplicateNotAllowed` ao criar Device ID e tratado como sucesso.
- `InvalidUser` leva ao fluxo de `CreateUser`.
- Quando o login termina com sucesso, o sistema:
  - armazena `ProductUserId`
  - marca `EOSManagerWrapper.SetConnected(true)`
  - chama `SessionManager.StartSession(...)`
  - dispara `OnLoginSuccess`

## API atual

- `LoginWithDeviceId()`
- `Logout()`
- `SetDeviceIdName(string)`
- `GetProductUserId()`
- eventos `OnLoginSuccess`, `OnLoginFailed` e `OnLogout`

## MPPM e clones

- `MppmHelper` detecta clones por `--virtual-project-clone`, `-vpId=` ou variavel de ambiente.
- `WindowsPlatformSpecifics` isola o cache/temp do EOS por clone.
- `SessionManager` gera um `sessionToken` por processo para ajudar a distinguir instancias.

## Troubleshooting

- EOS nao inicializado
- `ConnectInterface` ausente
- timeout de inicializacao
- credenciais invalidas
- falha na criacao do Device ID
