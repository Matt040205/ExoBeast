using UnityEngine;
using Unity.Netcode;

namespace ExoBeasts.Multiplayer.Sync
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedTrapVisual : NetworkBehaviour
    {
        [SerializeField] private float sellRefundPercentage = 0.6f;

        public NetworkVariable<ulong> BuilderClientId = new NetworkVariable<ulong>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> TrapIndex = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<ulong> LogicObjectId = new NetworkVariable<ulong>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private TrapDataSO trapData;

        public TrapDataSO TrapData
        {
            get
            {
                if (trapData == null)
                    ResolveTrapData();

                return trapData;
            }
        }
        public float SellRefundPercentage => sellRefundPercentage;

        public override void OnNetworkSpawn()
        {
            TrapIndex.OnValueChanged += OnTrapIndexChanged;
            ResolveTrapData();
        }

        public void InitializeServer(ulong builderClientId, int trapIndex, ulong logicObjectId)
        {
            if (!IsServer) return;

            BuilderClientId.Value = builderClientId;
            TrapIndex.Value = trapIndex;
            LogicObjectId.Value = logicObjectId;
            ResolveTrapData();
        }

        public bool CanInteractLocally()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return true;

            return BuilderClientId.Value == NetworkManager.Singleton.LocalClientId;
        }

        public void SellTrap()
        {
            RequestSellServerRpc();
        }

        [ServerRpc(RequireOwnership = false)]
        private void RequestSellServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer || !CanRequesterModify(rpcParams.Receive.SenderClientId))
                return;

            ResolveTrapData();
            if (trapData != null && CurrencyManager.Instance != null)
            {
                int geoditeRefund = Mathf.FloorToInt(trapData.geoditeCost * sellRefundPercentage);
                int etherRefund = Mathf.FloorToInt(trapData.darkEtherCost * sellRefundPercentage);

                if (geoditeRefund > 0)
                    CurrencyManager.Instance.AddCurrency(geoditeRefund, CurrencyType.Geodites);

                if (etherRefund > 0)
                    CurrencyManager.Instance.AddCurrency(etherRefund, CurrencyType.DarkEther);
            }

            if (LogicObjectId.Value != 0 &&
                NetworkManager.Singleton != null &&
                NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(LogicObjectId.Value, out NetworkObject logicObject))
            {
                logicObject.Despawn(true);
            }

            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);
            else
                Destroy(gameObject);
        }

        private void OnTrapIndexChanged(int oldValue, int newValue)
        {
            ResolveTrapData();
        }

        private void ResolveTrapData()
        {
            trapData = BuildManager.Instance?.GetTrapDataByIndex(TrapIndex.Value);
        }

        private bool CanRequesterModify(ulong senderClientId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return true;

            return senderClientId == BuilderClientId.Value;
        }

        public override void OnNetworkDespawn()
        {
            TrapIndex.OnValueChanged -= OnTrapIndexChanged;
        }
    }
}
