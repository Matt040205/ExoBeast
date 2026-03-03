# Instruções de Configuração - ExoBeasts Multiplayer

## 🎯 Arquitetura: P2P (Peer-to-Peer)

O projeto usa **P2P com Host** ao invés de Dedicated Server:
- Um jogador atua como **Host** (servidor + cliente)
- Outros jogadores conectam diretamente ao Host
- Epic P2P Service facilita conexões (NAT Traversal)

---

## 🔐 Passo 1: Configurar Credenciais Epic (CRÍTICO)

### 1.1 Criar Arquivo de Credenciais

Na **raiz do projeto** (mesmo nível que `Assets/`), crie:

```
EOSCredentials.json
```

### 1.2 Preencher Credenciais

Use este template (substitua com seus valores reais):

```json
{
  "ProductId": "sua_product_id_aqui",
  "SandboxId": "sua_sandbox_id_aqui",
  "DeploymentId": "seu_deployment_id_aqui",
  "ClientId": "sua_client_id_aqui",
  "ClientSecret": "sua_client_secret_aqui",
  "EncryptionKey": "sua_encryption_key_64_chars"
}
```

### 1.3 Onde Encontrar as Credenciais

Acesse: https://dev.epicgames.com/portal

1. **Product ID & Sandbox ID:** Product Settings
2. **Deployment ID:** Deployments → Development
3. **Client ID & Secret:** Clients → GameClient
4. **Encryption Key:** Game Services (opcional)

### 1.4 Segurança

✅ O arquivo `EOSCredentials.json` está no `.gitignore`
✅ Suas credenciais **NUNCA** serão enviadas ao GitHub
✅ **NÃO compartilhe** este arquivo publicamente

---

## 📦 Passo 2: Verificar Pacotes Unity

Os seguintes pacotes já foram adicionados ao `Packages/manifest.json`:

```json
"com.unity.netcode.gameobjects": "2.2.1",
"com.unity.transport": "2.4.0",
"com.unity.multiplayer.tools": "2.2.1"
```

Unity irá baixá-los automaticamente ao abrir o projeto.

---

## 🎮 Passo 3: Epic Developer Portal - Configuração P2P

No portal Epic, certifique-se de ativar:

### Game Services:
- ✅ **Lobbies** (obrigatório)
- ✅ **Peer-to-peer** (obrigatório para P2P)
- ❌ ~~Game Server Hosting~~ (NÃO necessário para P2P)

---

## 🗂️ Estrutura de Pastas Criada

```
Assets/Codigo/Multiplayer/
├── Core/
│   ├── NetworkBootstrap.cs        # Inicialização de rede
│   ├── EOSManager.cs               # SDK Epic Online Services
│   ├── EOSConfig.cs                # Configurações (carrega credenciais)
│   └── HostManager.cs              # Gerenciamento do Host P2P
├── Auth/
│   ├── EOSAuthenticator.cs         # Login/Autenticação
│   └── SessionManager.cs           # Sessão do usuário
├── Lobby/
│   ├── LobbyData.cs                # Estruturas de dados
│   ├── LobbyManager.cs             # CRUD de lobbies
│   ├── LobbyUI.cs                  # Interface de lobby
│   └── LobbyItemUI.cs              # Item da lista de lobbies
├── GameServer/
│   ├── GameServerManager.cs        # Lógica do Host
│   ├── MatchManager.cs             # Gerenciamento de partida
│   └── PlayerRegistry.cs           # Registro de jogadores
└── Sync/
    ├── NetworkedPlayerController.cs  # Sincronização de jogador
    ├── NetworkedCurrency.cs         # Moedas sincronizadas
    ├── NetworkedBuilding.cs         # Torres sincronizadas
    └── NetworkedHorde.cs            # Waves sincronizadas
```

---

## 🚀 Próximos Passos

### 1. Instalar EOS Plugin
- Importar PlayEveryWare EOS Plugin para Unity
- GitHub: https://github.com/PlayEveryWare/eos_plugin_for_unity

### 2. Criar ScriptableObject
1. Project → Create → Multiplayer → EOS Config
2. Salvar como `Assets/Resources/EOSConfig.asset`
3. No Inspector, clicar em "Load Credentials From File"

### 3. Testar Conexão
- Executar cena `NetworkBootstrap.unity`
- Verificar console para mensagens de autenticação

---

## ⚠️ Troubleshooting

### "Arquivo de credenciais não encontrado"
- Verifique se `EOSCredentials.json` está na raiz (não em Assets/)
- Caminho: `PI3D/EOSCredentials.json`

### "Credenciais incompletas"
- Verifique JSON (sem vírgulas extras)
- Todos os campos devem estar preenchidos

### Credenciais vazaram no Git?
1. **Revocar** imediatamente no portal Epic
2. Gerar novas credenciais
3. Limpar histórico do Git

---

## 📚 Documentação

- **Plano Completo:** `Assets/Codigo/Multiplayer/parallel-enchanting-harp.md`
- **Segurança:** `CREDENTIALS_SETUP.md`
- **Epic Docs:** https://dev.epicgames.com/docs/epic-online-services

---

## 🔧 Configurações de Desenvolvimento

### Modo P2P vs Dedicated Server
- ✅ Projeto configurado para **P2P**
- `NetworkBootstrap.useP2PMode = true`
- Host usa `NetworkManager.StartHost()`

### Build Settings
- **NÃO é necessário** build separado de servidor
- Host e Clientes usam o mesmo build
- Diferença é apenas no modo de inicialização

---

## 📞 Suporte

Dúvidas? Consulte:
- Epic Online Services Docs
- Unity Netcode for GameObjects Docs
- Equipe de desenvolvimento
