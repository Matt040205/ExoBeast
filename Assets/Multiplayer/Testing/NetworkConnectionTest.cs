using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;

namespace ExoBeasts.Multiplayer.Testing
{
    /// <summary>
    /// ── NetworkConnectionTest ────────────────────────────
    /// Teste de conexao P2P basica via NGO + UnityTransport, sem EOS Lobby.
    ///
    ///  ▸ StartAsHost(): escuta em 0.0.0.0 na porta configurada
    ///  ▸ StartAsClient(): conecta ao IP:porta do Inspector/OnGUI
    ///  ▸ LoadGameScene(): carrega cena para todos via NetworkSceneManager (apenas Host)
    ///  ▸ OnGUI de debug sem Canvas — cena: Network Test.unity
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class NetworkConnectionTest : MonoBehaviour
    {
        [Header("Configuracoes de Conexao")]
        [Tooltip("IP do Host para o Client se conectar")]
        [SerializeField] private string serverIp = "127.0.0.1";

        [Tooltip("Porta de rede (Host e Client devem usar a mesma)")]
        [SerializeField] private ushort serverPort = 7777;

        [Header("Cena de Jogo (Opcional)")]
        [Tooltip("Nome da cena a carregar apos conexao. Vazio = nao troca de cena.")]
        [SerializeField] private string gameSceneName = "";

        private string _statusMessage = "Pronto para conectar";
        private NetworkRole _currentRole = NetworkRole.None;

        private enum NetworkRole { None, Host, Client }

        private void Start()
        {
            if (NetworkManager.Singleton == null)
            {
                _statusMessage = "ERRO: NetworkManager nao encontrado na cena!";
                Debug.LogError("[NetworkConnectionTest] NetworkManager.Singleton is null. " +
                               "Adicione um GameObject com o componente NetworkManager na cena.");
                return;
            }

            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            Debug.Log("[NetworkConnectionTest] Pronto. Escolha Host ou Client na janela de debug.");
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        /// <summary>
        /// Inicia como Host P2P: este jogador e servidor + cliente ao mesmo tempo.
        /// </summary>
        public void StartAsHost()
        {
            var transport = GetTransport();
            if (transport == null) return;

            // "0.0.0.0" = escuta em todas as interfaces de rede da maquina
            transport.SetConnectionData("0.0.0.0", serverPort);

            bool success = NetworkManager.Singleton.StartHost();
            if (success)
            {
                _currentRole = NetworkRole.Host;
                _statusMessage = $"HOST ativo | Porta {serverPort} | Aguardando jogadores...";
                Debug.Log($"[NetworkConnectionTest] Host iniciado na porta {serverPort}");
            }
            else
            {
                _statusMessage = "ERRO: Falha ao iniciar Host. Verifique o Console.";
                Debug.LogError("[NetworkConnectionTest] NetworkManager.StartHost() falhou. " +
                               "Verifique se a porta nao esta em uso.");
            }
        }

        /// <summary>
        /// Inicia como Client: conecta ao Host no IP:Porta configurado.
        /// </summary>
        public void StartAsClient()
        {
            var transport = GetTransport();
            if (transport == null) return;

            transport.SetConnectionData(serverIp, serverPort);

            _statusMessage = $"Conectando a {serverIp}:{serverPort}...";
            _currentRole = NetworkRole.Client;
            Debug.Log($"[NetworkConnectionTest] Tentando conectar ao Host {serverIp}:{serverPort}");

            NetworkManager.Singleton.StartClient();
        }

        /// <summary>
        /// Para o Host ou desconecta o Client.
        /// </summary>
        public void Stop()
        {
            if (NetworkManager.Singleton == null) return;

            NetworkManager.Singleton.Shutdown();
            _currentRole = NetworkRole.None;
            _statusMessage = "Desconectado";
            Debug.Log("[NetworkConnectionTest] NetworkManager desligado.");
        }

        /// <summary>
        /// Carrega a cena de jogo via Network Scene Management.
        /// Todos os clientes conectados sao transportados automaticamente.
        /// Apenas o Host pode chamar isso.
        /// </summary>
        public void LoadGameScene()
        {
            if (string.IsNullOrEmpty(gameSceneName))
            {
                Debug.LogWarning("[NetworkConnectionTest] gameSceneName nao definido no Inspector.");
                return;
            }

            if (!NetworkManager.Singleton.IsHost)
            {
                Debug.LogWarning("[NetworkConnectionTest] Apenas o Host pode iniciar troca de cena.");
                return;
            }

            Debug.Log($"[NetworkConnectionTest] Carregando cena '{gameSceneName}' para todos os clientes...");

            NetworkManager.Singleton.SceneManager.LoadScene(gameSceneName, LoadSceneMode.Single);
        }

        private void OnServerStarted()
        {
            _statusMessage = $"HOST ativo | Porta: {serverPort}";
            Debug.Log("[NetworkConnectionTest] Servidor Host iniciado com sucesso.");
        }

        private void OnClientConnected(ulong clientId)
        {
            if (_currentRole == NetworkRole.Host)
            {
                int count = NetworkManager.Singleton.ConnectedClients.Count;
                _statusMessage = $"HOST | {count}/4 jogadores conectados";
                Debug.Log($"[NetworkConnectionTest] Cliente {clientId} conectou. Total: {count}");
            }
            else if (_currentRole == NetworkRole.Client &&
                     clientId == NetworkManager.Singleton.LocalClientId)
            {
                _statusMessage = $"Conectado ao Host! | Client ID: {clientId}";
                Debug.Log($"[NetworkConnectionTest] Conectado com sucesso. ClientId local: {clientId}");
            }
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (_currentRole == NetworkRole.Client &&
                clientId == NetworkManager.Singleton.LocalClientId)
            {
                _currentRole = NetworkRole.None;
                _statusMessage = "Desconectado do Host";
                Debug.Log("[NetworkConnectionTest] Desconectado do Host.");
            }
            else if (_currentRole == NetworkRole.Host)
            {
                int count = NetworkManager.Singleton.ConnectedClients.Count;
                _statusMessage = $"HOST | {count}/4 jogadores conectados";
                Debug.Log($"[NetworkConnectionTest] Cliente {clientId} desconectou. Restam: {count}");
            }
        }

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 420, 520));
            GUILayout.BeginVertical("box");

            GUILayout.Label("=== Teste de Conexao P2P - ExoBeasts ===");
            GUILayout.Space(5);

            GUILayout.Label($"Status: {_statusMessage}");
            GUILayout.Label($"Modo:   {_currentRole}");

            if (NetworkManager.Singleton != null)
            {
                GUILayout.Label($"IsHost:            {NetworkManager.Singleton.IsHost}");
                GUILayout.Label($"IsClient:          {NetworkManager.Singleton.IsClient}");
                GUILayout.Label($"IsConnectedClient: {NetworkManager.Singleton.IsConnectedClient}");

                if (NetworkManager.Singleton.IsHost)
                {
                    int count = NetworkManager.Singleton.ConnectedClients.Count;
                    GUILayout.Label($"Jogadores: {count}/4");
                }
            }

            GUILayout.Space(10);

            bool isRunning = NetworkManager.Singleton != null &&
                             (NetworkManager.Singleton.IsHost || NetworkManager.Singleton.IsClient);

            if (!isRunning)
            {
                GUILayout.Label("IP do Host (preencha no CLIENT):");
                serverIp = GUILayout.TextField(serverIp, GUILayout.Width(220));

                GUILayout.Label("Porta:");
                string portStr = GUILayout.TextField(serverPort.ToString(), GUILayout.Width(80));
                if (ushort.TryParse(portStr, out ushort parsed))
                    serverPort = parsed;

                GUILayout.Space(10);

                if (GUILayout.Button("Iniciar como HOST", GUILayout.Height(45)))
                    StartAsHost();

                GUILayout.Space(5);

                if (GUILayout.Button("Entrar como CLIENT", GUILayout.Height(45)))
                    StartAsClient();
            }
            else
            {
                if (GUILayout.Button("Parar / Desconectar", GUILayout.Height(35)))
                    Stop();

                if (NetworkManager.Singleton.IsHost && !string.IsNullOrEmpty(gameSceneName))
                {
                    GUILayout.Space(10);
                    GUILayout.Label($"Cena configurada: '{gameSceneName}'");
                    if (GUILayout.Button($"Carregar cena de jogo (todos os clients)", GUILayout.Height(40)))
                        LoadGameScene();
                }
                else if (NetworkManager.Singleton.IsHost && string.IsNullOrEmpty(gameSceneName))
                {
                    GUILayout.Space(10);
                    GUILayout.Label("[Inspector] Defina 'Game Scene Name' para habilitar\na troca de cena sincronizada.");
                }
            }

            GUILayout.Space(10);
            GUILayout.Label("Verifique o Console para logs detalhados.");

            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private UnityTransport GetTransport()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[NetworkConnectionTest] NetworkManager nao encontrado!");
                _statusMessage = "ERRO: NetworkManager ausente na cena";
                return null;
            }

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport == null)
            {
                Debug.LogError("[NetworkConnectionTest] UnityTransport nao encontrado no NetworkManager! " +
                               "Selecione o NetworkManager e configure o Network Transport para 'Unity Transport'.");
                _statusMessage = "ERRO: UnityTransport ausente no NetworkManager";
            }

            return transport;
        }
    }
}
