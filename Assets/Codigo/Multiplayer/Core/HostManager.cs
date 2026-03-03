using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Transports.UTP;

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// Gerencia a logica do Host em modo P2P
    /// Host = Servidor + Cliente ao mesmo tempo
    /// </summary>
    public class HostManager : MonoBehaviour
    {
        private static HostManager _instance;
        public static HostManager Instance => _instance;

        [Header("Host Settings")]
        [SerializeField] private ushort hostPort = 7777;
        [SerializeField] private int maxPlayers = 4;

        private bool isHost = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Iniciar como Host (servidor + cliente simultaneamente).
        /// Chamado quando o jogador cria um lobby e inicia a partida.
        /// </summary>
        public void StartAsHost()
        {
            Debug.Log($"[HostManager] Iniciando como Host (P2P) na porta {hostPort}...");

            var transport = NetworkManager.Singleton?.GetComponent<UnityTransport>();
            if (transport != null)
                transport.SetConnectionData("0.0.0.0", hostPort);

            if (NetworkManager.Singleton != null)
            {
                bool success = NetworkManager.Singleton.StartHost();
                if (success)
                {
                    isHost = true;
                    Debug.Log("[HostManager] Host iniciado com sucesso! Aguardando jogadores...");
                }
                else
                {
                    Debug.LogError("[HostManager] Falha ao iniciar Host!");
                }
            }
        }

        /// <summary>
        /// Iniciar como Client: conecta ao Host via IP.
        /// Chamado quando o jogador entra em um lobby e a partida comeca.
        /// </summary>
        /// <param name="hostIp">IP do Host (obtido via Lobby EOS)</param>
        /// <param name="port">Porta do Host (0 usa o hostPort padrao)</param>
        public void StartAsClient(string hostIp, ushort port = 0)
        {
            if (port == 0) port = hostPort;

            if (NetworkManager.Singleton == null)
            {
                Debug.LogError("[HostManager] NetworkManager ausente. Nao e possivel conectar como Client.");
                return;
            }

            var transport = NetworkManager.Singleton.GetComponent<UnityTransport>();
            if (transport != null)
                transport.SetConnectionData(hostIp, port);

            Debug.Log($"[HostManager] Iniciando como Client -> {hostIp}:{port}...");
            NetworkManager.Singleton.StartClient();
        }

        /// <summary>
        /// Para de ser Host e encerra o servidor local.
        /// </summary>
        public void StopHost()
        {
            if (!isHost) return;

            Debug.Log("[HostManager] Encerrando Host...");

            if (NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.Shutdown();
            }

            isHost = false;
        }

        public bool IsHost()
        {
            return isHost && NetworkManager.Singleton != null && NetworkManager.Singleton.IsHost;
        }

        public int GetMaxPlayers()
        {
            return maxPlayers;
        }

        public ushort GetHostPort()
        {
            return hostPort;
        }

        /// <summary>
        /// Obter numero de jogadores conectados (incluindo o host)
        /// </summary>
        public int GetConnectedPlayersCount()
        {
            if (NetworkManager.Singleton == null || !isHost) return 0;

            return (int)NetworkManager.Singleton.ConnectedClients.Count;
        }

        private void OnApplicationQuit()
        {
            if (isHost)
            {
                StopHost();
            }
        }
    }
}
