using UnityEngine;
using Unity.Netcode;
using System.Collections.Generic;

namespace ExoBeasts.Multiplayer.GameServer
{
    /// <summary>
    /// ── PlayerRegistry ───────────────────────────────────
    /// Registro server-side de jogadores conectados durante a partida.
    ///
    ///  ▸ Mapeia clientId → GameObject e clientId → NetworkObject
    ///  ▸ RegisterPlayer / UnregisterPlayer: chamados pelo NetworkedPlayerController
    ///  ▸ OnClientDisconnected: despawna automaticamente
    ///  ▸ GetLocalPlayer(): retorna objeto do jogador local
    ///  ▸ Singleton
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class PlayerRegistry : NetworkBehaviour
    {
        private static PlayerRegistry _instance;
        public static PlayerRegistry Instance => _instance;

        private Dictionary<ulong, GameObject> playerObjects = new Dictionary<ulong, GameObject>();
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
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

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

            var networkObject = playerObject.GetComponent<NetworkObject>();
            if (networkObject != null)
            {
                playerNetworkObjects.Add(clientId, networkObject);
            }

            Debug.Log($"[PlayerRegistry] Jogador {clientId} registrado. Total: {playerObjects.Count}");
        }

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

        public GameObject GetPlayerObject(ulong clientId)
        {
            if (playerObjects.ContainsKey(clientId))
            {
                return playerObjects[clientId];
            }
            return null;
        }

        public NetworkObject GetPlayerNetworkObject(ulong clientId)
        {
            if (playerNetworkObjects.ContainsKey(clientId))
            {
                return playerNetworkObjects[clientId];
            }
            return null;
        }

        public Dictionary<ulong, GameObject> GetAllPlayers()
        {
            return playerObjects;
        }

        public int GetPlayerCount()
        {
            return playerObjects.Count;
        }

        public bool IsPlayerRegistered(ulong clientId)
        {
            return playerObjects.ContainsKey(clientId);
        }

        public List<ulong> GetAllClientIds()
        {
            return new List<ulong>(playerObjects.Keys);
        }

        public GameObject GetLocalPlayer()
        {
            if (NetworkManager.Singleton == null) return null;

            ulong localClientId = NetworkManager.Singleton.LocalClientId;
            return GetPlayerObject(localClientId);
        }

        private void OnClientDisconnected(ulong clientId)
        {
            if (!IsServer) return;

            if (playerObjects.ContainsKey(clientId))
            {
                GameObject playerObj = playerObjects[clientId];

                // Despawn() ja destroi o GameObject por padrao
                if (playerObj != null)
                {
                    var networkObject = playerObj.GetComponent<NetworkObject>();
                    if (networkObject != null && networkObject.IsSpawned)
                    {
                        networkObject.Despawn();
                    }
                    else if (playerObj != null)
                    {
                        Destroy(playerObj);
                    }
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

        public void ClearRegistry()
        {
            if (!IsServer) return;

            playerObjects.Clear();
            playerNetworkObjects.Clear();
            Debug.Log("[PlayerRegistry] Registro limpo");
        }
    }
}
