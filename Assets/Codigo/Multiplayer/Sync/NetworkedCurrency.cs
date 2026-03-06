using UnityEngine;
using Unity.Netcode;
using System;

namespace ExoBeasts.Multiplayer.Sync
{
    /// <summary>
    /// ── NetworkedCurrency ────────────────────────────────
    /// Moedas do time sincronizadas em rede (Geodites e DarkEther).
    ///
    ///  ▸ NetworkVariables server-write: TeamGeodites, TeamDarkEther
    ///  ▸ Add/Spend ServerRpcs: ganho e gasto com validacao servidor
    ///  ▸ Aprovacao/rejeicao via ClientRpc para feedback do solicitante
    ///  ▸ HasEnoughGeodites/DarkEther: check local para UI imediata
    ///  ▸ Singleton
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class NetworkedCurrency : NetworkBehaviour
    {
        private static NetworkedCurrency _instance;
        public static NetworkedCurrency Instance => _instance;

        [Header("Team Currency")]
        public NetworkVariable<int> TeamGeodites = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> TeamDarkEther = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public event Action<int> OnGeoditesChanged;
        public event Action<int> OnDarkEtherChanged;

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
            TeamGeodites.OnValueChanged += OnGeoditesValueChanged;
            TeamDarkEther.OnValueChanged += OnDarkEtherValueChanged;

            Debug.Log("[NetworkedCurrency] Sistema de moedas inicializado");
        }

        private void OnGeoditesValueChanged(int oldValue, int newValue)
        {
            Debug.Log($"[NetworkedCurrency] Geodites: {oldValue} -> {newValue}");
            OnGeoditesChanged?.Invoke(newValue);
        }

        private void OnDarkEtherValueChanged(int oldValue, int newValue)
        {
            Debug.Log($"[NetworkedCurrency] DarkEther: {oldValue} -> {newValue}");
            OnDarkEtherChanged?.Invoke(newValue);
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddGeoditesServerRpc(int amount, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            TeamGeodites.Value += amount;
            Debug.Log($"[NetworkedCurrency] +{amount} Geodites. Total: {TeamGeodites.Value}");
        }

        [ServerRpc(RequireOwnership = false)]
        public void AddDarkEtherServerRpc(int amount, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            TeamDarkEther.Value += amount;
            Debug.Log($"[NetworkedCurrency] +{amount} DarkEther. Total: {TeamDarkEther.Value}");
        }

        [ServerRpc(RequireOwnership = false)]
        public void SpendGeoditesServerRpc(int amount, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong clientId = rpcParams.Receive.SenderClientId;

            if (TeamGeodites.Value >= amount)
            {
                TeamGeodites.Value -= amount;
                Debug.Log($"[NetworkedCurrency] -{amount} Geodites. Restante: {TeamGeodites.Value}");

                OnPurchaseApprovedClientRpc(clientId, PurchaseType.Geodites, amount);
            }
            else
            {
                Debug.LogWarning($"[NetworkedCurrency] Geodites insuficientes! Necessario: {amount}, Disponivel: {TeamGeodites.Value}");

                OnPurchaseRejectedClientRpc(clientId, PurchaseType.Geodites);
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void SpendDarkEtherServerRpc(int amount, ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            ulong clientId = rpcParams.Receive.SenderClientId;

            if (TeamDarkEther.Value >= amount)
            {
                TeamDarkEther.Value -= amount;
                Debug.Log($"[NetworkedCurrency] -{amount} DarkEther. Restante: {TeamDarkEther.Value}");

                OnPurchaseApprovedClientRpc(clientId, PurchaseType.DarkEther, amount);
            }
            else
            {
                Debug.LogWarning($"[NetworkedCurrency] DarkEther insuficiente! Necessario: {amount}, Disponivel: {TeamDarkEther.Value}");
                OnPurchaseRejectedClientRpc(clientId, PurchaseType.DarkEther);
            }
        }

        [ClientRpc]
        private void OnPurchaseApprovedClientRpc(ulong clientId, PurchaseType type, int amount)
        {
            if (NetworkManager.Singleton.LocalClientId != clientId) return;

            Debug.Log($"[NetworkedCurrency] Compra aprovada: {type} x{amount}");
        }

        [ClientRpc]
        private void OnPurchaseRejectedClientRpc(ulong clientId, PurchaseType type)
        {
            if (NetworkManager.Singleton.LocalClientId != clientId) return;

            Debug.LogWarning($"[NetworkedCurrency] Compra rejeitada: {type}");
        }

        public bool HasEnoughGeodites(int amount)
        {
            return TeamGeodites.Value >= amount;
        }

        public bool HasEnoughDarkEther(int amount)
        {
            return TeamDarkEther.Value >= amount;
        }

        public override void OnNetworkDespawn()
        {
            TeamGeodites.OnValueChanged -= OnGeoditesValueChanged;
            TeamDarkEther.OnValueChanged -= OnDarkEtherValueChanged;
        }
    }

    public enum PurchaseType
    {
        Geodites,
        DarkEther
    }
}
