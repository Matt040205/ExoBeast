# Guia de Autenticacao EOS - ExoBeasts V3

## Visao Geral

Este documento descreve o sistema de autenticacao implementado no **Passo 2.1** do plano multiplayer. O sistema utiliza **Epic Online Services (EOS)** com autenticacao via **Device ID**, permitindo login anonimo sem necessidade de conta Epic.

**Status:** Funcional (necessita polimentos)

---

## Pre-requisitos

### 1. Configuracao no Epic Developer Portal

Antes de usar o sistema, configure seu projeto no [Epic Developer Portal](https://dev.epicgames.com/portal):

1. Criar conta de desenvolvedor Epic
2. Criar nova Organizacao
3. Criar novo Produto "ExoBeasts"
4. Obter credenciais:
   - Product ID
   - Sandbox ID (Development)
   - Deployment ID
   - Client ID
   - Client Secret

### 2. Arquivo de Credenciais

Criar arquivo `EOSCredentials.json` na **raiz do projeto** (nao em Assets!):

```json
{
    "ProductId": "seu_product_id",
    "SandboxId": "seu_sandbox_id",
    "DeploymentId": "seu_deployment_id",
    "ClientId": "seu_client_id",
    "ClientSecret": "seu_client_secret",
    "EncryptionKey": "sua_chave_64_caracteres_hex"
}
```

**IMPORTANTE:** Este arquivo NAO deve ser commitado no Git!

### 3. Cena com EOSManager

A cena deve conter um GameObject com o componente `PlayEveryWare.EpicOnlineServices.EOSManager`.

---

## Arquitetura

```
┌─────────────────────────────────────────────────────────────┐
│                    WindowsPlatformSpecifics                  │
│   [RuntimeInitializeOnLoadMethod - BeforeSceneLoad]         │
│   Registra PlatformSpecifics no singleton do PlayEveryWare  │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                 PlayEveryWare EOSManager                     │
│   Plugin externo que gerencia o EOS SDK                     │
│   Inicializa automaticamente no Awake                       │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    EOSManagerWrapper                         │
│   Nosso wrapper para integracao                             │
│   - Carrega credenciais de arquivo externo                  │
│   - Fornece interfaces (Platform, Connect, Auth)            │
│   - Gerencia estado de inicializacao                        │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    EOSAuthenticator                          │
│   Gerencia autenticacao via Device ID                       │
│   - CreateDeviceId()                                        │
│   - Login()                                                 │
│   - CreateUser() (se necessario)                            │
└─────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────┐
│                    SessionManager                            │
│   Armazena dados da sessao do usuario                       │
│   - userId, displayName                                     │
│   - currentLobbyId, currentMatchId                          │
└─────────────────────────────────────────────────────────────┘
```

---

## Componentes Detalhados

### 1. WindowsPlatformSpecifics.cs

**Caminho:** `Assets/Codigo/Multiplayer/Core/WindowsPlatformSpecifics.cs`

**Proposito:** Workaround obrigatorio para Windows. O PlayEveryWare EOS Plugin nao fornece implementacao de `PlatformSpecifics` para Windows standalone.

**Caracteristicas:**
- Executa ANTES de qualquer cena
- Registra instancia no singleton do PlayEveryWare
- Desabilita RTC (Voice Chat) para simplificar

**Codigo-chave:**
```csharp
[RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
private static void Initialize()
{
    var instance = new WindowsPlatformSpecifics();
    EOSManagerPlatformSpecificsSingleton.SetEOSManagerPlatformSpecificsInterface(instance);
}
```

### 2. EOSManagerWrapper.cs

**Caminho:** `Assets/Codigo/Multiplayer/Core/EOSManager.cs`

**Proposito:** Camada de abstracao entre nosso codigo e o PlayEveryWare plugin.

**Responsabilidades:**
- Singleton persistente (`DontDestroyOnLoad`)
- Carrega credenciais de `EOSCredentials.json`
- Aguarda inicializacao do PlayEveryWare (timeout 10s)
- Fornece interfaces do EOS SDK
- Gerencia shutdown seguro

**Interfaces Expostas:**
```csharp
GetPlatformInterface()  // Interface principal do EOS
GetConnectInterface()   // Para Device ID auth
GetAuthInterface()      // Para Epic Account auth (futuro)
```

**Eventos:**
```csharp
event Action OnEOSInitialized;           // SDK pronto
event Action OnEOSShutdown;              // SDK desligado
event Action<string> OnInitializationFailed;  // Erro
```

### 3. EOSAuthenticator.cs

**Caminho:** `Assets/Codigo/Multiplayer/Auth/EOSAuthenticator.cs`

**Proposito:** Gerencia autenticacao via Device ID (login anonimo).

**Metodos Principais:**
```csharp
void LoginWithDeviceId()      // Inicia fluxo de login
void Logout()                 // Encerra sessao
void SetDeviceIdName(string)  // Define nome de exibicao
ProductUserId GetProductUserId()  // Obtem ID do usuario
```

**Propriedades:**
```csharp
bool IsLoggedIn              // Estado de login
string CurrentProductUserId  // ID do usuario (string)
```

**Eventos:**
```csharp
event Action<string> OnLoginSuccess;  // Login OK (ProductUserId)
event Action<string> OnLoginFailed;   // Erro (mensagem)
event Action OnLogout;                // Logout realizado
```

### 4. SessionManager.cs

**Caminho:** `Assets/Codigo/Multiplayer/Auth/SessionManager.cs`

**Proposito:** Armazena dados da sessao do usuario logado.

**Metodos:**
```csharp
void StartSession(userId, displayName)  // Inicia sessao
void EndSession()                       // Encerra sessao
void SetCurrentLobby(lobbyId)          // Define lobby atual
void SetCurrentMatch(matchId)          // Define partida atual
```

**Getters:**
```csharp
string GetUserId()
string GetDisplayName()
bool IsInSession()
bool IsInLobby()
bool IsInMatch()
```

### 5. EOSConfig.cs

**Caminho:** `Assets/Codigo/Multiplayer/Core/EOSConfig.cs`

**Proposito:** ScriptableObject para configuracao de credenciais.

**Metodos:**
```csharp
void LoadCredentialsFromFile()  // Carrega de EOSCredentials.json
bool ValidateCredentials()      // Valida se completas
void ClearCredentials()         // Limpa da memoria (seguranca)
```

---

## Fluxo de Autenticacao

### Diagrama de Sequencia

```
┌──────────────────────────────────────────────────────────────┐
│ 1. INICIALIZACAO                                             │
├──────────────────────────────────────────────────────────────┤
│ WindowsPlatformSpecifics.Initialize()                        │
│    └─> Registra PlatformSpecifics                           │
│                                                              │
│ PlayEveryWare.EOSManager.Awake()                            │
│    └─> Inicializa EOS SDK                                   │
│                                                              │
│ EOSManagerWrapper.Initialize()                               │
│    ├─> LoadCredentialsFromFile()                            │
│    ├─> ValidateCredentials()                                │
│    └─> Aguarda PlayEveryWare (timeout 10s)                  │
│        └─> OnEOSInitialized ou OnInitializationFailed       │
└──────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────────┐
│ 2. LOGIN COM DEVICE ID                                       │
├──────────────────────────────────────────────────────────────┤
│ EOSAuthenticator.LoginWithDeviceId()                         │
│    ├─> Verifica EOS inicializado                            │
│    ├─> Obtem ConnectInterface                               │
│    └─> CreateDeviceIdAndLogin()                             │
│                                                              │
│ CreateDeviceIdOptions:                                       │
│    DeviceModel = SystemInfo.deviceModel + "_" + deviceName  │
│                                                              │
│ connectInterface.CreateDeviceId()                            │
│    └─> Callback: OnCreateDeviceIdComplete()                 │
│        ├─> Result.Success: Novo Device ID                   │
│        ├─> Result.DuplicateNotAllowed: Ja existe (OK)       │
│        └─> Outro: Erro                                      │
└──────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────────┐
│ 3. AUTENTICACAO                                              │
├──────────────────────────────────────────────────────────────┤
│ PerformDeviceIdLogin()                                       │
│    Credentials:                                              │
│       Type = ExternalCredentialType.DeviceidAccessToken     │
│       Token = null                                          │
│    UserLoginInfo:                                            │
│       DisplayName = deviceIdName                            │
│                                                              │
│ connectInterface.Login()                                     │
│    └─> Callback: OnConnectLoginComplete()                   │
│        ├─> Result.Success: Login OK                         │
│        ├─> Result.InvalidUser: Criar usuario                │
│        └─> Outro: Erro                                      │
└──────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────────┐
│ 4. CRIAR USUARIO (SE NECESSARIO)                            │
├──────────────────────────────────────────────────────────────┤
│ CreateUser(continuanceToken)                                 │
│    └─> connectInterface.CreateUser()                        │
│        └─> Callback: OnCreateUserComplete()                 │
│            ├─> Result.Success: Usuario criado               │
│            └─> Outro: Erro                                  │
└──────────────────────────────────────────────────────────────┘
                            │
                            ▼
┌──────────────────────────────────────────────────────────────┐
│ 5. FINALIZACAO                                               │
├──────────────────────────────────────────────────────────────┤
│ Sucesso:                                                     │
│    ├─> Armazena localProductUserId                          │
│    ├─> isLoggedIn = true                                    │
│    ├─> EOSManagerWrapper.SetConnected(true)                 │
│    ├─> SessionManager.StartSession()                        │
│    └─> OnLoginSuccess(productUserId)                        │
└──────────────────────────────────────────────────────────────┘
```

---

## Como Usar

### Exemplo 1: Login Automatico

```csharp
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Multiplayer.Auth;

public class GameInitializer : MonoBehaviour
{
    void Start()
    {
        // Configurar eventos
        EOSAuthenticator.Instance.OnLoginSuccess += OnLoginOK;
        EOSAuthenticator.Instance.OnLoginFailed += OnLoginError;

        // Iniciar login
        StartCoroutine(InitializeAndLogin());
    }

    IEnumerator InitializeAndLogin()
    {
        // Aguardar EOS inicializar
        EOSManagerWrapper.Instance.Initialize();

        while (!EOSManagerWrapper.Instance.IsInitialized)
        {
            yield return null;
        }

        // Fazer login
        EOSAuthenticator.Instance.SetDeviceIdName("MeuJogador");
        EOSAuthenticator.Instance.LoginWithDeviceId();
    }

    void OnLoginOK(string userId)
    {
        Debug.Log($"Logado como: {userId}");
        // Carregar proxima cena...
    }

    void OnLoginError(string error)
    {
        Debug.LogError($"Erro no login: {error}");
    }
}
```

### Exemplo 2: Verificar Estado

```csharp
// Verificar se esta logado
if (EOSAuthenticator.Instance.IsLoggedIn)
{
    string userId = EOSAuthenticator.Instance.CurrentProductUserId;
    Debug.Log($"Usuario logado: {userId}");
}

// Verificar dados da sessao
if (SessionManager.Instance.IsInSession())
{
    string name = SessionManager.Instance.GetDisplayName();
    Debug.Log($"Jogador: {name}");
}
```

### Exemplo 3: Logout

```csharp
EOSAuthenticator.Instance.OnLogout += () => {
    Debug.Log("Logout realizado");
    // Voltar ao menu...
};

EOSAuthenticator.Instance.Logout();
```

---

## Eventos e Callbacks

### Tabela de Eventos

| Classe | Evento | Quando Dispara | Parametro |
|--------|--------|----------------|-----------|
| EOSManagerWrapper | OnEOSInitialized | SDK pronto para uso | - |
| EOSManagerWrapper | OnEOSShutdown | SDK desligado | - |
| EOSManagerWrapper | OnInitializationFailed | Erro na inicializacao | string (motivo) |
| EOSAuthenticator | OnLoginSuccess | Login bem-sucedido | string (ProductUserId) |
| EOSAuthenticator | OnLoginFailed | Falha em qualquer etapa | string (mensagem) |
| EOSAuthenticator | OnLogout | Logout realizado | - |

### Ordem de Eventos (Sucesso)

```
1. OnEOSInitialized
2. OnLoginSuccess
3. (uso do sistema...)
4. OnLogout
5. OnEOSShutdown
```

---

## Troubleshooting

### Erro: "EOS nao inicializado"

**Causa:** `EOSManagerWrapper.Initialize()` nao foi chamado ou falhou.

**Solucao:**
1. Verificar se o EOSManager do PlayEveryWare esta na cena
2. Verificar credenciais em `EOSCredentials.json`
3. Verificar logs no console

### Erro: "ConnectInterface nao disponivel"

**Causa:** PlayEveryWare nao inicializou corretamente.

**Solucao:**
1. Verificar `StreamingAssets/EOS/` contendo configs
2. Verificar se `WindowsPlatformSpecifics` esta sendo carregado
3. Aumentar timeout em `WaitForPlayEveryWareInit()`

### Erro: "Timeout aguardando EOS"

**Causa:** PlayEveryWare demora mais de 10s para inicializar.

**Solucao:**
1. Verificar conexao de internet
2. Verificar configuracoes do portal Epic
3. Verificar logs do PlayEveryWare

### Erro: "Credenciais invalidas"

**Causa:** `EOSCredentials.json` mal formatado ou incompleto.

**Solucao:**
1. Verificar JSON valido
2. Verificar todos os campos preenchidos
3. Verificar se IDs correspondem ao portal Epic

### Erro: "Device ID falhou"

**Causa:** Problema ao criar Device ID no EOS.

**Solucao:**
1. Verificar se Client tem permissao para Device ID no portal
2. Verificar `DeviceModel` nao esta vazio
3. Tentar novamente (erro temporario de rede)

---

## Polimentos Necessarios

### Criticos

1. **Retry Automatico**
   - Implementar tentativa automatica em caso de falha de rede
   - Limite de 3 tentativas com backoff exponencial

2. **Tratamento de Erros Robusto**
   - Alguns ResultCodes do EOS nao sao tratados
   - Adicionar logging detalhado para debug

3. **Timeout Configuravel**
   - Expor timeout como parametro
   - Atualmente fixo em 10 segundos

### Melhorias Futuras

1. **Login com Epic Account**
   - Implementar `LoginWithEpicAccount()` para producao
   - Permite vincular progresso a conta Epic

2. **Persistencia de Sessao**
   - Salvar token de sessao localmente
   - Reconectar automaticamente ao reabrir jogo

3. **Validacao Periodica**
   - Verificar conexao EOS periodicamente
   - Disparar evento se conexao perdida

4. **UI de Feedback**
   - Criar componentes UI para mostrar estado
   - Loading spinner durante login

---

## Proximos Passos

Apos concluir o Passo 2.1, o proximo passo e:

### Passo 2.2: UI de Login

Criar interface de usuario para:
- Mostrar estado de conexao
- Permitir definir nome do jogador
- Botoes de login/logout
- Feedback visual de erros

### Passo 3: Sistema de Lobby

Implementar:
- Criar/buscar/entrar lobbies
- Lista de jogadores
- Selecao de personagem
- Sistema de "pronto"

---

## Referencias

- [Epic Online Services Documentation](https://dev.epicgames.com/docs)
- [PlayEveryWare EOS Plugin](https://github.com/PlayEveryWare/eos_plugin_for_unity)
- [Unity Netcode for GameObjects](https://docs-multiplayer.unity3d.com)
- Plano completo: `parallel-enchanting-harp.md`

---

**Versao:** 1.0
**Ultima Atualizacao:** Janeiro 2025
**Autor:** Sistema de Documentacao ExoBeasts
