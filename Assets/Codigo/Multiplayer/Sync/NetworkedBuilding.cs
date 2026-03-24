using UnityEngine;
using Unity.Netcode;

namespace ExoBeasts.Multiplayer.Sync
{
    /// <summary>
    /// ── NetworkedBuilding ────────────────────────────────
    /// Sincroniza estado de torres e armadilhas em rede.
    ///
    ///  ▸ NetworkVariables: Type, Level, Health, IsActive (server-write)
    ///  ▸ TakeDamageServerRpc: dano de inimigos, destroi ao chegar a 0
    ///  ▸ UpgradeServerRpc: incrementa Level (max 3), notifica via ClientRpc
    ///  ▸ RepairServerRpc: restaura Health ate o maximo
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class NetworkedBuilding : NetworkBehaviour
    {
        [Header("Building Data")]
        public NetworkVariable<BuildingType> Type = new NetworkVariable<BuildingType>(
            BuildingType.Tower,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> Level = new NetworkVariable<int>(
            1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<float> Health = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<bool> IsActive = new NetworkVariable<bool>(
            true,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[NetworkedBuilding] Spawned - Type: {Type.Value}, Level: {Level.Value}");

            Level.OnValueChanged += OnLevelChanged;
            Health.OnValueChanged += OnHealthChanged;
            IsActive.OnValueChanged += OnActiveChanged;

            if (IsServer)
            {
                InitializeServerData();
            }
        }

        private void InitializeServerData()
        {
        }

        private void OnLevelChanged(int oldValue, int newValue)
        {
            Debug.Log($"[NetworkedBuilding] Level mudou: {oldValue} -> {newValue}");
        }

        private void OnHealthChanged(float oldValue, float newValue)
        {
            Debug.Log($"[NetworkedBuilding] Vida mudou: {oldValue} -> {newValue}");

            if (newValue <= 0 && oldValue > 0)
            {
                OnBuildingDestroyed();
            }
        }

        private void OnActiveChanged(bool oldValue, bool newValue)
        {
            Debug.Log($"[NetworkedBuilding] Ativo mudou: {oldValue} -> {newValue}");
        }

        [ServerRpc(RequireOwnership = false)]
        public void TakeDamageServerRpc(float damage, ulong attackerId)
        {
            if (!IsServer) return;

            Health.Value = Mathf.Max(0, Health.Value - damage);
            Debug.Log($"[NetworkedBuilding] Dano recebido: {damage}. Vida: {Health.Value}");

            if (Health.Value <= 0)
            {
                DestroyBuilding();
            }
        }

        [ServerRpc(RequireOwnership = false)]
        public void UpgradeServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            if (Level.Value >= 3)
            {
                Debug.LogWarning("[NetworkedBuilding] Nivel maximo atingido");
                return;
            }

            Level.Value++;
            Debug.Log($"[NetworkedBuilding] Upgrade para nivel {Level.Value}");
            OnBuildingUpgradedClientRpc(Level.Value);
        }

        [ClientRpc]
        private void OnBuildingUpgradedClientRpc(int newLevel)
        {
            Debug.Log($"[NetworkedBuilding] Efeito de upgrade para nivel {newLevel}");
        }

        private void DestroyBuilding()
        {
            if (!IsServer) return;

            Debug.Log("[NetworkedBuilding] Construcao destruida");
            IsActive.Value = false;
            OnBuildingDestroyedClientRpc();

            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn();
                Destroy(gameObject, 2f);
            }
        }

        private void OnBuildingDestroyed()
        {
            Debug.Log("[NetworkedBuilding] Building destruido localmente");
        }

        [ClientRpc]
        private void OnBuildingDestroyedClientRpc()
        {
            Debug.Log("[NetworkedBuilding] Efeito de destruicao");
        }

        [ServerRpc(RequireOwnership = false)]
        public void RepairServerRpc(float amount)
        {
            if (!IsServer) return;

            float maxHealth = 100f;
            Health.Value = Mathf.Min(maxHealth, Health.Value + amount);
            Debug.Log($"[NetworkedBuilding] Reparado: +{amount}. Vida: {Health.Value}");
        }

        public override void OnNetworkDespawn()
        {
            Level.OnValueChanged -= OnLevelChanged;
            Health.OnValueChanged -= OnHealthChanged;
            IsActive.OnValueChanged -= OnActiveChanged;
        }
    }

    public enum BuildingType
    {
        Tower,
        Trap,
        Wall,
        Special
    }
}
