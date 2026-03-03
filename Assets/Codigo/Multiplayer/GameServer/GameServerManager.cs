using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace ExoBeasts.Multiplayer.GameServer
{
    /// <summary>
    /// Gerencia a logica do servidor dedicado
    /// Apenas executa no servidor, nao nos clientes
    /// </summary>
    public class GameServerManager : NetworkBehaviour
    {
        private static GameServerManager _instance;
        public static GameServerManager Instance => _instance;

        [Header("Server Settings")]
        [SerializeField] private int maxConnectedPlayers = 4;
        [SerializeField] private float serverTickRate = 60f;

        private Dictionary<ulong, PlayerData> connectedPlayers = new Dictionary<ulong, PlayerData>();
        private bool isServerRunning = false;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                InitializeServer();
            }
        }

        private void InitializeServer()
        {
            Debug.Log("[GameServerManager] Inicializando servidor de jogo...");

            // Registrar callbacks de conexao
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;

            isServerRunning = true;
            Debug.Log("[GameServerManager] Servidor pronto e aguardando jogadores");
        }

        private void OnClientConnected(ulong clientId)
        {
            Debug.Log($"[GameServerManager] Cliente conectado: {clientId}");

            // Validar se ha espaco
            if (connectedPlayers.Count >= maxConnectedPlayers)
            {
                Debug.LogWarning($"[GameServerManager] Servidor cheio! Desconectando cliente {clientId}");
                NetworkManager.Singleton.DisconnectClient(clientId);
                return;
            }

            // Registrar jogador
            var playerData = new PlayerData
            {
                clientId = clientId,
                displayName = $"Player_{clientId}",
                isReady = false
            };

            connectedPlayers.Add(clientId, playerData);
            Debug.Log($"[GameServerManager] Jogadores conectados: {connectedPlayers.Count}/{maxConnectedPlayers}");

            // Notificar outros jogadores
            NotifyPlayerJoinedClientRpc(clientId, playerData.displayName);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            Debug.Log($"[GameServerManager] Cliente desconectado: {clientId}");

            if (connectedPlayers.ContainsKey(clientId))
            {
                connectedPlayers.Remove(clientId);
                Debug.Log($"[GameServerManager] Jogadores restantes: {connectedPlayers.Count}");

                // Notificar outros jogadores
                NotifyPlayerLeftClientRpc(clientId);
            }

            // Se todos desconectaram, encerrar servidor
            if (connectedPlayers.Count == 0)
            {
                Debug.Log("[GameServerManager] Todos os jogadores desconectaram. Encerrando servidor...");
                // TODO: Encerrar servidor gracefully
            }
        }

        [ClientRpc]
        private void NotifyPlayerJoinedClientRpc(ulong clientId, string displayName)
        {
            Debug.Log($"[GameServerManager] Notificacao: Jogador {displayName} entrou");
        }

        [ClientRpc]
        private void NotifyPlayerLeftClientRpc(ulong clientId)
        {
            Debug.Log($"[GameServerManager] Notificacao: Jogador {clientId} saiu");
        }

        /// <summary>
        /// Validar se um jogador pode realizar uma acao
        /// </summary>
        public bool ValidatePlayerAction(ulong clientId, string action)
        {
            if (!IsServer) return false;

            if (!connectedPlayers.ContainsKey(clientId))
            {
                Debug.LogWarning($"[GameServerManager] Cliente {clientId} nao esta registrado");
                return false;
            }

            // TODO: Adicionar validacoes especificas por acao
            return true;
        }

        /// <summary>
        /// Obter dados de um jogador conectado
        /// </summary>
        public PlayerData GetPlayerData(ulong clientId)
        {
            if (connectedPlayers.ContainsKey(clientId))
            {
                return connectedPlayers[clientId];
            }
            return null;
        }

        /// <summary>
        /// Obter todos os jogadores conectados
        /// </summary>
        public Dictionary<ulong, PlayerData> GetAllPlayers()
        {
            return connectedPlayers;
        }

        public bool IsServerReady()
        {
            return isServerRunning;
        }

        private void OnDestroy()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }
    }

    /// <summary>
    /// Dados de um jogador conectado
    /// </summary>
    [System.Serializable]
    public class PlayerData
    {
        public ulong clientId;
        public string displayName;
        public int characterIndex;
        public bool isReady;
        public float connectionTime;

        public PlayerData()
        {
            clientId = 0;
            displayName = "Player";
            characterIndex = 0;
            isReady = false;
            connectionTime = Time.time;
        }
    }
}
