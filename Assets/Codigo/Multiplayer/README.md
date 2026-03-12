# Sistema Multiplayer - ExoBeasts V3

## Visao Geral

Sistema multiplayer usando **Unity Netcode for GameObjects (NGO 1.12.0)** + **Epic Online Services (EOS)**.

| Item | Valor |
|------|-------|
| **Arquitetura** | P2P com Host |
| **Max Jogadores** | 4 |
| **Transport** | Unity Transport 2.4.0 (UDP) |
| **Matchmaking** | EOS Lobby Service |
| **NAT Traversal** | EOS P2P Service (a configurar) |

---

## Estrutura de Pastas

```
Multiplayer/
├── Core/
│   ├── NetworkBootstrap.cs       ✅ Inicializacao de rede (StartHost/StartClient reais)
│   ├── EOSManager.cs             ✅ Wrapper do EOS SDK (PlayEveryWare)
│   ├── EOSConfig.cs              ✅ Carrega credenciais do arquivo externo
│   ├── HostManager.cs            ✅ StartAsHost() e StartAsClient()
│   ├── WindowsPlatformSpecifics.cs ✅ Workaround Windows EOS
│   ├── EOSConfig_Main.asset      ✅ ScriptableObject de config
│   └── EOSManager.prefab         ✅ Prefab do manager
│
├── Auth/
│   ├── EOSAuthenticator.cs       ✅ Login via Device ID (funcional)
│   └── SessionManager.cs         ✅ Sessao do usuario
│
├── Lobby/
│   ├── LobbyData.cs              ✅ Structs (LobbyInfo, LobbyMember, etc.)
│   ├── LobbyManager.cs           ✅ Chamadas EOS reais (Create, Search, Join, Leave, StartMatch)
│   ├── LobbyUI.cs                ✅ Canvas UI principal do lobby
│   └── LobbyItemUI.cs            ✅ Item da lista de lobbies
│
├── GameServer/
│   ├── GameServerManager.cs      ✅ Gerencia conexoes do servidor
│   ├── MatchManager.cs           🚧 Estrutura criada
│   └── PlayerRegistry.cs         ✅ Mapeia clientId → GameObject
│
├── Sync/
│   ├── NetworkedPlayerController.cs 🚧 Estrutura criada
│   ├── NetworkedCurrency.cs         🚧 Estrutura criada
│   ├── NetworkedBuilding.cs         🚧 Estrutura criada
│   ├── NetworkedHorde.cs            ✅ Wave system sincronizado (server-authoritative)
│   ├── NetworkedEnemy.cs            ✅ Wrapper de rede para inimigos (saude, morte, IA so no servidor)
│   └── PlayerNetworkSetup.cs        ✅ Habilita/desabilita controles por papel de rede (owner vs remoto)
│
├── Testing/
│   ├── EOSAuthTest.cs            ✅ Teste de autenticacao EOS (funcional)
│   └── NetworkConnectionTest.cs  ✅ Teste de conexao Host/Client (funcional)
│
├── Docs/
│   ├── AUTHENTICATION_GUIDE.md   ✅ Guia de autenticacao
│   ├── SETUP_INSTRUCTIONS.md     ✅ Guia de configuracao
│   └── CREDENTIALS_SETUP.md      ✅ Seguranca de credenciais
│
├── EOSAuthTest.unity             ✅ Cena de teste EOS Auth
├── EOSCredentials.json.example   ✅ Template de credenciais
├── README.md                     (este arquivo)
└── parallel-enchanting-harp.md   ✅ Plano detalhado completo
```

---

## Como Comecar

### 1. Credenciais Epic

Leia: `CREDENTIALS_SETUP.md`

Resumo: crie `EOSCredentials.json` na **raiz do projeto** (nao em Assets!) e preencha com as credenciais do Epic Developer Portal.

### 2. Testar Autenticacao

1. Abra a cena `EOSAuthTest.unity`
2. Pressione Play
3. O login via Device ID acontece automaticamente
4. Verifique no Console: `[EOSAuthTest] Login bem-sucedido! ProductUserId: ...`

### 3. Testar Conexao P2P

1. Abra a cena `NetworkTest.unity`
2. Instale o Multiplayer Play Mode: `Window → Package Manager → Add by name: com.unity.multiplayer.playmode`
3. Configure 2 virtual players em `Window → Multiplayer → Multiplayer Play Mode`
4. Pressione Play → em uma janela clique **HOST**, na outra clique **CLIENT**

---

## Fluxo de Jogo P2P

```
[Login EOS] → [Criar/Entrar Lobby EOS] → [Todos prontos]
                                                 ↓
                                    Host: HostManager.StartAsHost()
                                    Clients: HostManager.StartAsClient(ip)
                                                 ↓
                                    NetworkManager.SceneManager.LoadScene()
                                    (todos os clients carregam juntos)
```

---

## Status de Desenvolvimento

### ✅ Fase 1: Fundacao (Concluido)
- [x] Epic Developer Portal configurado
- [x] NGO 1.12.0 + UnityTransport 2.4.0 instalados
- [x] EOS Plugin (PlayEveryWare) instalado
- [x] Estrutura de pastas e scripts base criados
- [x] Sistema de credenciais seguro (fora do repositorio)

### ✅ Fase 2: Autenticacao Device ID (Concluido)
- [x] EOSManagerWrapper implementado
- [x] EOSAuthenticator com Device ID funcional
- [x] SessionManager implementado
- [x] Cena de teste `EOSAuthTest.unity` funcionando
- [x] Login anonimo confirmado em execucao

### ✅ Sprint: Validacao de Rede Basica (Concluido)
- [x] NetworkBootstrap.cs implementado (StartHost/StartClient reais)
- [x] HostManager.cs com StartAsHost() e StartAsClient()
- [x] NetworkConnectionTest.cs criado (UI de debug via OnGUI)
- [x] NetworkManager + UnityTransport configurados na cena de teste
- [x] Player Prefab (capsula) com NetworkObject + NetworkTransform
- [x] Teste com 2 instancias: Host e Client conectados com sucesso
- [x] Capsulas spawnando corretamente para cada jogador
- [x] Troca de cena sincronizada via NGO SceneManager testada

### ✅ Fase 3: Lobby System (Concluido — Marco 2026)
- [x] LobbyManager com chamadas EOS reais (CreateLobby, JoinLobby, Search, Leave, StartMatch)
- [x] LobbyScene.unity com UI de placeholder (LobbyPlaceholderUI.cs via OnGUI)
- [x] Atributos de membro: DISPLAY_NAME, SESSION_TOKEN, READY
- [x] Host inicia partida → publica SERVER_ADDRESS → clients detectam e chamam StartClient()
- [x] Sistema hibrido de identidade ClientId + ProductUserId (anti-colisao MPPM)
- [x] MppmHelper.cs — deteccao de clone MPPM via args de linha de comando
- [x] PlayerIdentityBridge.cs — ponte NGO ClientId ↔ EOS ProductUserId via ServerRpc

### ✅ Sprint: NavMesh na Rede (Concluido — Marco 2026)
- [x] EnemyController integrado com NavMeshAgent (caminho inteligente ao redor de obstaculos)
- [x] NavMeshAgent desabilitado em clientes — IA roda apenas no servidor (host)
- [x] Knockback: desabilita agent temporariamente, usa Rigidbody.AddForce, restaura apos 0.4s
- [x] NetworkedEnemy.cs — saude e morte como NetworkVariables; TakeDamageServerRpc acessivel por qualquer jogador
- [x] EnemyPoolManager.cs — bug de pool criando apenas 1 inimigo corrigido

### ✅ Sprint: Sincronizacao de Movimento e Animacao (Concluido — Marco 2026)
- [x] PlayerNetworkSetup.cs — habilita controles apenas no owner; desabilita CharacterController no remoto
- [x] NetworkedHorde.cs — SpawnEnemy() completo com pontos de spawn configuraveis no Inspector
- [x] NetworkedHorde.cs — EnemiesRemaining protegido com Mathf.Max(0, ...) contra underflow

### 🚧 Fase 4: P2P via Internet (Em andamento)
- [x] LAN funcional (NGO + UnityTransport UDP)
- [ ] EOS P2P Transport (NAT Traversal para conexoes fora da LAN)
- [ ] Teste entre maquinas em redes diferentes (prioridade maxima)

### 🔲 Fase 5: Sincronizacao de Gameplay
- [ ] Prefabs de personagem (Samurai, Arqueira) configurados no Editor:
  - NetworkObject + ClientNetworkTransform (owner-authoritative)
  - NetworkAnimator para sincronizar animacoes de ataque/corrida
  - PlayerNetworkSetup com refs preenchidas no Inspector
- [ ] PlayerMovement.cs e PlayerHealthSystem.cs migrados para NetworkBehaviour
- [ ] NavMesh bake na cena SceneMapTest.unity (aguardando level design)
- [ ] MONSTRO.prefab com NavMeshAgent + NetworkObject + NetworkTransform + NetworkedEnemy

### 🔲 Fase 6: Polimento
- [ ] Tratamento de desconexao e reconexao
- [ ] Teste de performance com 4 jogadores simultaneos
- [ ] Timeout em operacoes async EOS (UI trava se backend inacessivel)

---

## Seguranca

**NUNCA commitar:**
- `EOSCredentials.json` (credenciais reais)
- `.env` files com tokens

**Sempre verificar `.gitignore` antes de commits!**

---

## Referencias

- **Plano Detalhado:** `parallel-enchanting-harp.md`
- **EOS Docs:** https://dev.epicgames.com/docs
- **NGO Docs:** https://docs-multiplayer.unity3d.com
- **EOS Lobby:** https://dev.epicgames.com/docs/game-services/lobbies
- **EOS P2P:** https://dev.epicgames.com/docs/game-services/p-2-p

---

**Versao:** 2.0
**Ultima atualizacao:** Marco 2026
**Fase atual:** Fase 3 + Sprints NavMesh e Sync concluidos — Fase 4 (P2P Internet) e Fase 5 (Gameplay Sync) em andamento
