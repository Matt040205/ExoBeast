using UnityEngine;
using Unity.Netcode;

namespace ExoBeasts.Multiplayer.Sync
{
    [RequireComponent(typeof(NetworkObject))]
    public class NetworkedBuilding : NetworkBehaviour
    {
        [SerializeField] private TowerController towerController;

        public NetworkVariable<ulong> BuilderClientId = new NetworkVariable<ulong>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> CharacterIndex = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> TotalCostSpent = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> DpsLevel = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> ControlLevel = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<int> SupportLevel = new NetworkVariable<int>(
            0,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        public NetworkVariable<bool> IsActive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server);

        private int _lastAppliedSignature = int.MinValue;

        private void Awake()
        {
            RuntimeBuildableSanitizer.Sanitize(gameObject, false);

            if (towerController == null)
                towerController = GetComponent<TowerController>();
        }

        public override void OnNetworkSpawn()
        {
            base.OnNetworkSpawn();
            RuntimeBuildableSanitizer.Sanitize(gameObject, false);

            if (towerController == null)
                towerController = GetComponent<TowerController>();

            DpsLevel.OnValueChanged += OnAnyStateChanged;
            ControlLevel.OnValueChanged += OnAnyStateChanged;
            SupportLevel.OnValueChanged += OnAnyStateChanged;
            TotalCostSpent.OnValueChanged += OnAnyStateChanged;
            IsActive.OnValueChanged += OnActiveChanged;

            ApplySynchronizedState();
        }

        public void RefreshVisualState()
        {
            ApplySynchronizedState();
        }

        public void InitializeTowerServer(ulong builderClientId, int characterIndex, int initialCostSpent)
        {
            if (!IsServer) return;

            BuilderClientId.Value = builderClientId;
            CharacterIndex.Value = characterIndex;
            TotalCostSpent.Value = initialCostSpent;
            DpsLevel.Value = 0;
            ControlLevel.Value = 0;
            SupportLevel.Value = 0;
            IsActive.Value = true;
            ApplySynchronizedState();
        }

        public bool CanInteractLocally()
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return true;

            return BuilderClientId.Value == NetworkManager.Singleton.LocalClientId;
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestUpgradeServerRpc(int pathIndex, ServerRpcParams rpcParams = default)
        {
            if (!IsServer || !IsActive.Value || !CanRequesterModify(rpcParams.Receive.SenderClientId))
                return;

            if (towerController == null)
                towerController = GetComponent<TowerController>();

            if (towerController == null || towerController.towerData == null || towerController.towerData.upgradePaths == null)
                return;

            if (pathIndex < 0 || pathIndex >= towerController.towerData.upgradePaths.Count)
                return;

            UpgradePath path = towerController.towerData.upgradePaths[pathIndex];
            if (path == null || path.upgradesInPath == null)
                return;

            int currentLevel = GetPathLevel(pathIndex);
            int totalPointsSpent = DpsLevel.Value + ControlLevel.Value + SupportLevel.Value;
            if (totalPointsSpent >= 6 || currentLevel >= path.upgradesInPath.Count)
                return;

            Upgrade nextUpgrade = path.upgradesInPath[currentLevel];
            int geoditeCost = nextUpgrade.geoditeCost;
            int darkEtherCost = nextUpgrade.darkEtherCost;

            if (CurrencyManager.Instance == null ||
                !CurrencyManager.Instance.HasEnoughCurrency(geoditeCost, CurrencyType.Geodites) ||
                !CurrencyManager.Instance.HasEnoughCurrency(darkEtherCost, CurrencyType.DarkEther))
            {
                return;
            }

            if (geoditeCost > 0)
                CurrencyManager.Instance.SpendCurrency(geoditeCost, CurrencyType.Geodites);

            if (darkEtherCost > 0)
                CurrencyManager.Instance.SpendCurrency(darkEtherCost, CurrencyType.DarkEther);

            SetPathLevel(pathIndex, currentLevel + 1);
            TotalCostSpent.Value += geoditeCost + darkEtherCost;
            ApplySynchronizedState();
        }

        [ServerRpc(RequireOwnership = false)]
        public void RequestSellServerRpc(float refundPercentage, ServerRpcParams rpcParams = default)
        {
            if (!IsServer || !CanRequesterModify(rpcParams.Receive.SenderClientId))
                return;

            if (CurrencyManager.Instance != null)
            {
                int refundAmount = Mathf.FloorToInt(TotalCostSpent.Value * refundPercentage);
                if (refundAmount > 0)
                    CurrencyManager.Instance.AddCurrency(refundAmount, CurrencyType.Geodites);
            }

            IsActive.Value = false;

            if (NetworkObject != null && NetworkObject.IsSpawned)
                NetworkObject.Despawn(true);
            else
                Destroy(gameObject);
        }

        private void OnAnyStateChanged(int oldValue, int newValue)
        {
            ApplySynchronizedState();
        }

        private void OnActiveChanged(bool oldValue, bool newValue)
        {
            if (!newValue && gameObject != null)
                gameObject.SetActive(false);
        }

        private void ApplySynchronizedState()
        {
            if (towerController == null)
                towerController = GetComponent<TowerController>();

            if (towerController == null || towerController.towerData == null)
                return;

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = true;

            int signature =
                (DpsLevel.Value * 1000000) +
                (ControlLevel.Value * 100000) +
                (SupportLevel.Value * 10000) +
                TotalCostSpent.Value;

            if (_lastAppliedSignature == signature)
                return;

            _lastAppliedSignature = signature;
            towerController.ApplyNetworkUpgradeState(
                DpsLevel.Value,
                ControlLevel.Value,
                SupportLevel.Value,
                TotalCostSpent.Value);
        }

        private bool CanRequesterModify(ulong senderClientId)
        {
            if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
                return true;

            return senderClientId == BuilderClientId.Value;
        }

        private int GetPathLevel(int pathIndex)
        {
            if (pathIndex == 0) return DpsLevel.Value;
            if (pathIndex == 1) return ControlLevel.Value;
            if (pathIndex == 2) return SupportLevel.Value;
            return 0;
        }

        private void SetPathLevel(int pathIndex, int level)
        {
            if (pathIndex == 0) DpsLevel.Value = level;
            else if (pathIndex == 1) ControlLevel.Value = level;
            else if (pathIndex == 2) SupportLevel.Value = level;
        }

        [ClientRpc]
        public void BroadcastShieldVisualStateClientRpc(ulong targetNetId, bool isActive)
        {
            var shieldBehavior = GetComponent<DragonShieldGeneratorBehavior>();
            if (shieldBehavior != null)
            {
                shieldBehavior.ApplyShieldVisualStateLocal(targetNetId, isActive);
            }
        }

        public override void OnNetworkDespawn()
        {
            DpsLevel.OnValueChanged -= OnAnyStateChanged;
            ControlLevel.OnValueChanged -= OnAnyStateChanged;
            SupportLevel.OnValueChanged -= OnAnyStateChanged;
            TotalCostSpent.OnValueChanged -= OnAnyStateChanged;
            IsActive.OnValueChanged -= OnActiveChanged;
            base.OnNetworkDespawn();
        }
    }
}
