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
        private Dictionary<ulong, int> playerCharacterChoices = new Dictionary<ulong, int>();
        private Dictionary<ulong, string> playerUserIds = new Dictionary<ulong, string>();
        private Dictionary<string, ulong> userIdToClientId = new Dictionary<string, ulong>();
        private readonly List<ulong> invalidPlayerIds = new List<ulong>();
        private const string TAG_POCA = "Poca";

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
            if (!HasServerAuthority()) return;
            if (playerObj == null) return;

            if (playerObjects.TryGetValue(clientId, out var existing) && existing != null && existing != playerObj)
            {
                Debug.LogWarning(
                    $"[PlayerRegistry] RegisterPlayer sobrescrevendo jogador existente para clientId={clientId}. " +
                    $"Anterior: {existing.name}, novo: {playerObj.name}. " +
                    "Possivel duplo spawn — verificar GameSetupManager.");
            }
            playerObjects[clientId] = playerObj;

            var netObj = playerObj.GetComponent<NetworkObject>();
            if (netObj != null)
                playerNetworkObjects[clientId] = netObj;

            playerCharacterChoices[clientId] = characterIndex;
        }

        public void SetPlayerCharacterChoice(ulong clientId, int index)
        {
            if (!HasServerAuthority()) return;
            playerCharacterChoices[clientId] = index;
        }

        public int GetPlayerCharacterChoice(ulong clientId)
        {
            if (playerCharacterChoices.TryGetValue(clientId, out int index))
                return index;
            return 0;
        }

        public void LinkProductUserId(ulong clientId, string productUserId)
        {
            if (!HasServerAuthority()) return;
            playerUserIds[clientId] = productUserId;
            userIdToClientId[productUserId] = clientId;
            Debug.Log($"[PlayerRegistry] Link: ClientId={clientId} ↔ UserId={productUserId}");
        }

        public string GetProductUserId(ulong clientId)
        {
            return playerUserIds.TryGetValue(clientId, out string uid) ? uid : null;
        }

        public ulong? GetClientIdByUserId(string productUserId)
        {
            return userIdToClientId.TryGetValue(productUserId, out ulong cid) ? cid : null;
        }

        public void UnregisterPlayer(ulong clientId)
        {
            if (!HasServerAuthority()) return;

            if (playerUserIds.TryGetValue(clientId, out string uid))
            {
                userIdToClientId.Remove(uid);
                playerUserIds.Remove(clientId);
            }
            playerObjects.Remove(clientId);
            playerNetworkObjects.Remove(clientId);
            playerCharacterChoices.Remove(clientId);
        }

        public GameObject GetPlayerObject(ulong clientId)
        {
            PruneInvalidPlayers();
            if (playerObjects.TryGetValue(clientId, out GameObject obj))
                return obj;
            return null;
        }

        public Dictionary<ulong, GameObject> GetAllPlayers()
        {
            PruneInvalidPlayers();
            return playerObjects;
        }

        public int GetPlayerCount()
        {
            PruneInvalidPlayers();
            return playerObjects.Count;
        }

        public Transform GetClosestPlayer(Vector3 position)
        {
            float minDist = float.MaxValue;
            Transform closest = null;
            PruneInvalidPlayers();
            foreach (var p in playerObjects.Values)
            {
                if (!IsValidPlayerObject(p)) continue;
                float dist = Vector3.Distance(position, p.transform.position);
                if (dist < minDist)
                {
                    minDist = dist;
                    closest = p.transform;
                }
            }
            return closest;
        }

        public static int CollectValidPlayerTransforms(List<Transform> results)
        {
            if (results == null)
                return 0;

            results.Clear();
            CollectNetworkPlayerObjects(results);

            if (results.Count == 0 && Instance != null)
                Instance.CollectRegisteredPlayers(results);

            if (results.Count == 0)
                CollectTaggedPlayers(results);

            return results.Count;
        }

        public static bool IsValidPlayerObject(GameObject playerObject)
        {
            if (playerObject == null || !playerObject.activeInHierarchy || playerObject.CompareTag(TAG_POCA))
                return false;

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager != null && networkManager.IsListening &&
                playerObject.TryGetComponent(out NetworkObject networkObject) &&
                !networkObject.IsSpawned)
            {
                return false;
            }

            return true;
        }

        private static void CollectNetworkPlayerObjects(List<Transform> results)
        {
            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null || !networkManager.IsListening)
                return;

            foreach (NetworkClient client in networkManager.ConnectedClientsList)
            {
                NetworkObject playerObject = client.PlayerObject;
                if (playerObject == null || !IsValidPlayerObject(playerObject.gameObject))
                    continue;

                AddUnique(results, playerObject.transform);
            }
        }

        private void CollectRegisteredPlayers(List<Transform> results)
        {
            PruneInvalidPlayers();
            foreach (GameObject playerObject in playerObjects.Values)
            {
                if (IsValidPlayerObject(playerObject))
                    AddUnique(results, playerObject.transform);
            }
        }

        private static void CollectTaggedPlayers(List<Transform> results)
        {
            GameObject[] taggedPlayers = GameObject.FindGameObjectsWithTag("Player");
            foreach (GameObject playerObject in taggedPlayers)
            {
                if (IsValidPlayerObject(playerObject))
                    AddUnique(results, playerObject.transform);
            }
        }

        private static void AddUnique(List<Transform> results, Transform candidate)
        {
            if (candidate == null || results.Contains(candidate))
                return;

            results.Add(candidate);
        }

        private bool HasServerAuthority()
        {
            return IsServer || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer);
        }

        private void PruneInvalidPlayers()
        {
            invalidPlayerIds.Clear();

            foreach (KeyValuePair<ulong, GameObject> entry in playerObjects)
            {
                if (!IsValidPlayerObject(entry.Value))
                    invalidPlayerIds.Add(entry.Key);
            }

            foreach (ulong clientId in invalidPlayerIds)
            {
                playerObjects.Remove(clientId);
                playerNetworkObjects.Remove(clientId);
                playerCharacterChoices.Remove(clientId);

                if (playerUserIds.TryGetValue(clientId, out string uid))
                {
                    playerUserIds.Remove(clientId);
                    userIdToClientId.Remove(uid);
                }
            }
        }
    }
}
