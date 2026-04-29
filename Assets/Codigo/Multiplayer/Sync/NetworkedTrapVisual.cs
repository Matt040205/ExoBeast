using Unity.Netcode;
using UnityEngine;

namespace ExoBeasts.Multiplayer.Sync
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedTrapVisual : NetworkBehaviour
    {
        [SerializeField] private float sellRefundPercentage = 0.6f;
        [SerializeField] private string activationTrigger = "Ativar";
        [SerializeField] private string deactivationTrigger = "Desativar";

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

        public NetworkVariable<bool> IsActivated = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private TrapDataSO trapData;
        private Animator cachedAnimator;
        private bool isBeingSold;

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
            IsActivated.OnValueChanged += OnActivationChanged;
            ResolveTrapData();
            ResolveAnimator();
            ApplyActivationState(IsActivated.Value);
        }

        public override void OnNetworkDespawn()
        {
            if (IsServer && !isBeingSold && BuildManager.Instance != null)
                BuildManager.Instance.DecrementTrapCount(TrapIndex.Value);

            TrapIndex.OnValueChanged -= OnTrapIndexChanged;
            IsActivated.OnValueChanged -= OnActivationChanged;
            base.OnNetworkDespawn();
        }

        public void InitializeServer(ulong builderClientId, int trapIndex, ulong logicObjectId)
        {
            if (!IsServer)
                return;

            BuilderClientId.Value = builderClientId;
            TrapIndex.Value = trapIndex;
            LogicObjectId.Value = logicObjectId;
            ResolveTrapData();
        }

        public void SetActivationStateServer(bool isActivated)
        {
            if (!IsServer)
                return;

            IsActivated.Value = isActivated;
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

            if (BuildManager.Instance != null)
                BuildManager.Instance.DecrementTrapCount(TrapIndex.Value);
            isBeingSold = true;

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

        private void OnActivationChanged(bool oldValue, bool newValue)
        {
            ApplyActivationState(newValue);
        }

        private void ApplyActivationState(bool isActivated)
        {
            ResolveAnimator();
            if (cachedAnimator == null)
                return;

            cachedAnimator.SetTrigger(isActivated ? activationTrigger : deactivationTrigger);
        }

        private void ResolveAnimator()
        {
            if (cachedAnimator == null)
                cachedAnimator = GetComponentInChildren<Animator>(true);
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
    }
}
