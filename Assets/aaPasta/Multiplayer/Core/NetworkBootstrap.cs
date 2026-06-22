using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine.SceneManagement;
using ExoBeasts.Managers;
using ExoBeasts.Managers.Loading;

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// ── NetworkBootstrap ─────────────────────────────────
    /// Ponto de entrada para inicializacao do NGO (Netcode for GameObjects).
    ///
    ///  ▸ Registra callbacks do NetworkManager (Connected, Disconnected, ServerStarted)
    ///  ▸ StartHost(): configura UnityTransport em 0.0.0.0 e inicia como Host P2P
    ///  ▸ StartClient(ip): conecta ao Host no IP fornecido
    ///  ▸ autoStartHost / autoStartClient: flags para testes rapidos no Editor
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class NetworkBootstrap : MonoBehaviour
    {
        private const string MENU_SCENE_NAME = "MenuScene";

        [Header("Auto-inicio (para testes)")]
        [Tooltip("Iniciar automaticamente como Host ao entrar na cena")]
        [SerializeField] private bool autoStartHost = false;

        [Tooltip("Iniciar automaticamente como Client ao entrar na cena")]
        [SerializeField] private bool autoStartClient = false;

        [Header("Modo P2P")]
        [Tooltip("Em modo P2P, sempre use Host ao inves de Dedicated Server")]
        [SerializeField] private bool useP2PMode = true;

        [Header("Configuracao de Rede")]
        [Tooltip("IP do Host para o Client se conectar (usado em autoStartClient)")]
        [SerializeField] private string clientConnectIp = "127.0.0.1";

        [Tooltip("Porta de rede usada por Host e Client")]
        [SerializeField] private ushort networkPort = 7777;

        private bool returningToMenuAfterHostDisconnect;

        private void Awake()
        {
            transform.SetParent(null); // DDOL requer root GameObject
            DontDestroyOnLoad(gameObject);
            Debug.Log($"[NetworkBootstrap] Inicializando sistema de rede... (P2P: {useP2PMode})");
        }

        private void Start()
        {
            InitializeNetworking();

            if (autoStartHost && useP2PMode)
                StartHost();
            else if (autoStartClient)
                StartClient();
        }

        private void OnDestroy()
        {
            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnServerStarted -= OnServerStarted;
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
                NetworkManager.Singleton.OnClientStopped -= OnClientStopped;
                NetworkManager.Singleton.OnTransportFailure -= OnTransportFailure;
            }
        }

        private void InitializeNetworking()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[NetworkBootstrap] NetworkManager nao encontrado! " +
                               "Adicione um GameObject com o componente NetworkManager na cena.");
                return;
            }

            NetworkManager.Singleton.OnServerStarted += OnServerStarted;
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.Singleton.OnClientStopped += OnClientStopped;
            NetworkManager.Singleton.OnTransportFailure += OnTransportFailure;

            Debug.Log("[NetworkBootstrap] Callbacks de rede registrados com sucesso.");
        }

        /// <summary>
        /// Inicia como Host P2P: este jogador e servidor + cliente simultaneamente.
        /// Chamado pelo LobbyManager quando o jogador cria uma sala e inicia a partida.
        /// </summary>
        public void StartHost()
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[NetworkBootstrap] NetworkManager ausente. Nao e possivel iniciar Host.");
                return;
            }

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
                transport.SetConnectionData("0.0.0.0", networkPort);

            Debug.Log($"[NetworkBootstrap] Iniciando como Host na porta {networkPort}...");
            NetworkManager.Singleton.StartHost();
        }

        /// <summary>
        /// Inicia como Client: conecta ao Host.
        /// Chamado pelo LobbyManager quando o jogador entra numa sala.
        /// </summary>
        public void StartClient(string hostIp = null)
        {
            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[NetworkBootstrap] NetworkManager ausente. Nao e possivel iniciar Client.");
                return;
            }

            string ip = string.IsNullOrEmpty(hostIp) ? clientConnectIp : hostIp;

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
                transport.SetConnectionData(ip, networkPort);

            Debug.Log($"[NetworkBootstrap] Iniciando como Client -> {ip}:{networkPort}");
            NetworkManager.Singleton.StartClient();
        }

        /// <summary>Para a conexao atual (Host ou Client).</summary>
        public void Shutdown()
        {
            Debug.Log("[NetworkBootstrap] Encerrando conexao de rede...");
            if (NetworkManager.Singleton != null)
                NetworkManager.Singleton.Shutdown();
        }

        /// <summary>DEPRECATED: Use StartHost() no modelo P2P.</summary>
        public void StartServer()
        {
            Debug.LogWarning("[NetworkBootstrap] StartServer() esta deprecated! " +
                             "No modelo P2P use StartHost() - o host e servidor e cliente ao mesmo tempo.");
        }

        private void OnServerStarted()
        {
            Debug.Log("[NetworkBootstrap] Host/Servidor iniciado com sucesso!");
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[NetworkBootstrap] Cliente conectou | ClientId: {clientId}");
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[NetworkBootstrap] Cliente desconectou | ClientId: {clientId}");

            if (clientId == NetworkManager.ServerClientId)
                TryReturnClientToMenuAfterHostDisconnect("host disconnect callback");
        }

        private void OnClientStopped(bool wasHost)
        {
            if (!wasHost)
                TryReturnClientToMenuAfterHostDisconnect("client stopped");
        }

        private void OnTransportFailure()
        {
            TryReturnClientToMenuAfterHostDisconnect("transport failure");
        }

        private void TryReturnClientToMenuAfterHostDisconnect(string reason)
        {
            if (returningToMenuAfterHostDisconnect)
                return;

            if (GameModeManager.CurrentMode != GameMode.Multiplayer)
                return;

            if (SceneManager.GetActiveScene().name == MENU_SCENE_NAME)
                return;

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && (networkManager.IsServer || networkManager.IsHost))
                return;

            Debug.LogWarning($"[NetworkBootstrap] Host indisponivel ({reason}). Encerrando cliente e voltando para {MENU_SCENE_NAME}.");
            StartCoroutine(ReturnClientToMenuAfterHostDisconnect());
        }

        private System.Collections.IEnumerator ReturnClientToMenuAfterHostDisconnect()
        {
            returningToMenuAfterHostDisconnect = true;

            if (LoadingScreenUI.Instance != null)
                LoadingScreenUI.Instance.ForceHide();

            PauseControl.isPaused = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            MultiplayerRuntimeReset.ApplyOfflineLocalState();

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null &&
                (networkManager.IsListening ||
                 networkManager.IsClient ||
                 networkManager.IsHost ||
                 networkManager.IsServer))
            {
                networkManager.Shutdown();

                float elapsed = 0f;
                while (networkManager != null && networkManager.IsListening && elapsed < 2f)
                {
                    elapsed += Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Time.deltaTime;
                    yield return null;
                }
            }

            SceneManager.LoadScene(MENU_SCENE_NAME, LoadSceneMode.Single);
            returningToMenuAfterHostDisconnect = false;
        }
    }
}
