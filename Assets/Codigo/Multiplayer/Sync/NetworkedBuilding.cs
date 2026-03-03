using UnityEngine;
using Unity.Netcode;

namespace ExoBeasts.Multiplayer.Sync
{
    /// <summary>
    /// Componente de rede para torres e armadilhas
    /// Sincroniza construcao, upgrade e destruicao
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

        // Referencia ao componente local (se houver)
        // TODO: Adicionar referencia aos componentes de torre quando existirem
        // private TowerBase towerComponent;
        // private TrapBase trapComponent;

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[NetworkedBuilding] Spawned - Type: {Type.Value}, Level: {Level.Value}");

            // Buscar componentes locais
            // towerComponent = GetComponent<TowerBase>();
            // trapComponent = GetComponent<TrapBase>();

            // Escutar mudancas
            Level.OnValueChanged += OnLevelChanged;
            Health.OnValueChanged += OnHealthChanged;
            IsActive.OnValueChanged += OnActiveChanged;

            // Sincronizar valores iniciais se for servidor
            if (IsServer)
            {
                InitializeServerData();
            }
        }

        private void InitializeServerData()
        {
            // TODO: Sincronizar dados iniciais dos componentes locais
            // if (towerComponent != null)
            // {
            //     Health.Value = towerComponent.maxHealth;
            // }
        }

        private void OnLevelChanged(int oldValue, int newValue)
        {
            Debug.Log($"[NetworkedBuilding] Level mudou: {oldValue} -> {newValue}");
            // TODO: Atualizar visual do upgrade
        }

        private void OnHealthChanged(float oldValue, float newValue)
        {
            Debug.Log($"[NetworkedBuilding] Vida mudou: {oldValue} -> {newValue}");

            // Verificar se foi destruido
            if (newValue <= 0 && oldValue > 0)
            {
                OnBuildingDestroyed();
            }
        }

        private void OnActiveChanged(bool oldValue, bool newValue)
        {
            Debug.Log($"[NetworkedBuilding] Ativo mudou: {oldValue} -> {newValue}");
            // TODO: Habilitar/desabilitar funcionalidade
        }

        /// <summary>
        /// Receber dano (chamado por inimigos)
        /// </summary>
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

        /// <summary>
        /// Fazer upgrade da construcao
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void UpgradeServerRpc(ServerRpcParams rpcParams = default)
        {
            if (!IsServer) return;

            // Verificar se pode fazer upgrade
            if (Level.Value >= 3) // Max level
            {
                Debug.LogWarning("[NetworkedBuilding] Nivel maximo atingido");
                return;
            }

            Level.Value++;
            Debug.Log($"[NetworkedBuilding] Upgrade para nivel {Level.Value}");

            // Notificar todos os clientes
            OnBuildingUpgradedClientRpc(Level.Value);
        }

        [ClientRpc]
        private void OnBuildingUpgradedClientRpc(int newLevel)
        {
            Debug.Log($"[NetworkedBuilding] Efeito de upgrade para nivel {newLevel}");
            // TODO: Reproduzir efeito visual de upgrade
            // TODO: Atualizar modelo 3D
        }

        private void DestroyBuilding()
        {
            if (!IsServer) return;

            Debug.Log("[NetworkedBuilding] Construcao destruida");
            IsActive.Value = false;

            // Notificar clientes
            OnBuildingDestroyedClientRpc();

            // Despawnar e destruir
            if (NetworkObject != null && NetworkObject.IsSpawned)
            {
                NetworkObject.Despawn();
                Destroy(gameObject, 2f); // Delay para animacao
            }
        }

        private void OnBuildingDestroyed()
        {
            Debug.Log("[NetworkedBuilding] Building destruido localmente");
            // TODO: Reproduzir animacao de destruicao
        }

        [ClientRpc]
        private void OnBuildingDestroyedClientRpc()
        {
            Debug.Log("[NetworkedBuilding] Efeito de destruicao");
            // TODO: Reproduzir efeito de explosao
            // TODO: Reproduzir som de destruicao
        }

        /// <summary>
        /// Reparar construcao
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void RepairServerRpc(float amount)
        {
            if (!IsServer) return;

            float maxHealth = 100f; // TODO: Obter do componente local
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
        Tower,      // Torre de defesa
        Trap,       // Armadilha
        Wall,       // Muro/Barricada
        Special     // Construcao especial
    }
}
