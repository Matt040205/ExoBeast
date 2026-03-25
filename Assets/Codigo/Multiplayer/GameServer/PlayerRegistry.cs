using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

namespace ExoBeasts.Multiplayer.GameServer
{
    /// <summary>
    /// ── PlayerRegistry ─────────────────────────────────────
    /// Registro central de todos os jogadores conectados (server-only writes).
    ///
    ///  ▸ RegisterPlayer / UnregisterPlayer: gerencia dicionarios de jogadores
    ///  ▸ GetClosestPlayer(pos): retorna Transform do jogador mais proximo
    ///  ▸ GetPlayerCharacterChoice(clientId): retorna indice do personagem escolhido
    ///  ▸ Usado por EnemyController, HordeManager e GameSetupManager
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class PlayerRegistry : NetworkBehaviour
    {
        public static PlayerRegistry Instance { get; private set; }

        private Dictionary<ulong, GameObject> playerObjects = new Dictionary<ulong, GameObject>();
        private Dictionary<ulong, NetworkObject> playerNetworkObjects = new Dictionary<ulong, NetworkObject>();
        private Dictionary<ulong, int> playerCharacterChoices = new Dictionary<ulong, int>(); // NOVO

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else Destroy(gameObject);
        }

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            }
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer)
            {
                NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
            }
            base.OnNetworkDespawn();
        }

        private void OnClientDisconnected(ulong clientId)
        {
            UnregisterPlayer(clientId);
        }

        public void RegisterPlayer(ulong clientId, GameObject playerObj, int characterIndex = 0)
        {
            if (!IsServer) return;

            if (playerObjects.ContainsKey(clientId))
                playerObjects[clientId] = playerObj;
            else
                playerObjects.Add(clientId, playerObj);

            var netObj = playerObj.GetComponent<NetworkObject>();
            if (netObj != null)
                playerNetworkObjects[clientId] = netObj;
                
            playerCharacterChoices[clientId] = characterIndex;

        }

        public void SetPlayerCharacterChoice(ulong clientId, int index)
        {
            if (!IsServer) return;
            playerCharacterChoices[clientId] = index;
        }

        public int GetPlayerCharacterChoice(ulong clientId)
        {
            if (playerCharacterChoices.TryGetValue(clientId, out int index))
                return index;
            return 0;
        }

        public void UnregisterPlayer(ulong clientId)
        {
            if (!IsServer) return;

            playerObjects.Remove(clientId);
            playerNetworkObjects.Remove(clientId);
            playerCharacterChoices.Remove(clientId);
        }

        public GameObject GetPlayerObject(ulong clientId)
        {
            if (playerObjects.TryGetValue(clientId, out GameObject obj))
                return obj;
            return null;
        }

        public Dictionary<ulong, GameObject> GetAllPlayers() => playerObjects;

        public int GetPlayerCount() => playerObjects.Count;

        public Transform GetClosestPlayer(Vector3 position)
        {
            float minDist = float.MaxValue;
            Transform closest = null;
            foreach (var p in playerObjects.Values)
            {
                if (p == null) continue;
                float dist = Vector3.Distance(position, p.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = p.transform;
                }
            }
            return closest;
        }
    }
}
