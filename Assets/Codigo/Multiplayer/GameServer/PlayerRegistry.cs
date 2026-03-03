using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace ExoBeasts.Multiplayer.GameServer
{
    /// <summary>
    /// Registro centralizado de jogadores conectados
    /// Mapeia clientId para GameObject do jogador
    /// </summary>
    public class PlayerRegistry : NetworkBehaviour
    {
        private static PlayerRegistry _instance;
        public static PlayerRegistry Instance => _instance;

        // Mapeamento de clientId para GameObject do jogador
        private Dictionary<ulong, GameObject> playerObjects = new Dictionary<ulong, GameObject>();

        // Mapeamento de clientId para NetworkObject do jogador
        private Dictionary<ulong, NetworkObject> playerNetworkObjects = new Dictionary<ulong, NetworkObject>();

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
                // Registrar callbacks
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        /// <summary>
        /// Registrar um jogador no registro
        /// </summary>
        public void RegisterPlayer(ulong clientId, GameObject playerObject)
        {
            if (!IsServer)
            {
                Debug.LogWarning("[PlayerRegistry] Apenas o servidor pode registrar jogadores");
                return;
            }

            if (playerObjects.ContainsKey(clientId))
            {
                Debug.LogWarning($"[PlayerRegistry] Jogador {clientId} ja esta registrado");
                return;
            }

            playerObjects.Add(clientId, playerObject);

            // Registrar NetworkObject tambem
            var networkObject = playerObject.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                playerNetworkObjects.Add(clientId, networkObject);
            }

            Debug.Log($"[PlayerRegistry] Jogador {clientId} registrado. Total: {playerObjects.Count}");
        }

        /// <summary>
        /// Remover um jogador do registro
        /// </summary>
        public void UnregisterPlayer(ulong clientId)
        {
            if (!IsServer) return;

            if (playerObjects.ContainsKey(clientId))
            {
                playerObjects.Remove(clientId);
                playerNetworkObjects.Remove(clientId);
                Debug.Log($"[PlayerRegistry] Jogador {clientId} removido. Total: {playerObjects.Count}");
            }
        }

        /// <summary>
        /// Obter GameObject de um jogador pelo clientId
        /// </summary>
        public GameObject GetPlayerObject(ulong clientId)
        {
            if (playerObjects.ContainsKey(clientId))
            {
                return playerObjects[clientId];
            }
            return null;
        }

        /// <summary>
        /// Obter NetworkObject de um jogador pelo clientId
        /// </summary>
        public NetworkObject GetPlayerNetworkObject(ulong clientId)
        {
            if (playerNetworkObjects.ContainsKey(clientId))
            {
                return playerNetworkObjects[clientId];
            }
            return null;
        }

        /// <summary>
        /// Obter todos os jogadores conectados
        /// </summary>
        public Dictionary<ulong, GameObject> GetAllPlayers()
        {
            return playerObjects;
        }

        /// <summary>
        /// Obter numero de jogadores conectados
        /// </summary>
        public int GetPlayerCount()
        {
            return playerObjects.Count;
        }

        /// <summary>
        /// Verificar se um jogador esta registrado
        /// </summary>
        public bool IsPlayerRegistered(ulong clientId)
        {
            return playerObjects.ContainsKey(clientId);
        }

        /// <summary>
        /// Obter lista de todos os clientIds
        /// </summary>
        public List<ulong> GetAllClientIds()
        {
            return new List<ulong>(playerObjects.Keys);
        }

        /// <summary>
        /// Obter jogador local (o dono desta instancia)
        /// </summary>
        public GameObject GetLocalPlayer()
        {
            if (NetworkManager.Singleton == null) return null;

            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            return GetPlayerObject(localClientId);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            // Remover jogador do registro quando desconectar
            if (playerObjects.ContainsKey(clientId))
            {
                GameObject playerObj = playerObjects[clientId];

                // Despawnar e destruir o objeto do jogador
                if (playerObj != null)
                {
                    var networkObject = playerObj.GetComponent<NetworkObject>();
                    if (networkObject != null && networkObject.IsSpawned)
                    {
                        networkObject.Despawn();
                    }
                    Destroy(playerObj);
                }

                UnregisterPlayer(clientId);
            }
        }

        private void OnDestroy()
        {
            if (IsServer && NetworkManager.Singleton != null)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
        }

        /// <summary>
        /// Limpar todos os registros (util para resetar partida)
        /// </summary>
        public void ClearRegistry()
        {
            if (!IsServer) return;

            playerObjects.Clear();
            playerNetworkObjects.Clear();
            Debug.Log("[PlayerRegistry] Registro limpo");
        }
    }
}
