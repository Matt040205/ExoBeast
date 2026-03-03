using UnityEngine;
using Unity.Netcode;

namespace ExoBeasts.Multiplayer.Sync
{
    /// <summary>
    /// Controller de rede para o jogador
    /// Conecta os sistemas locais do jogador com a rede
    /// </summary>
    public class NetworkedPlayerController : NetworkBehaviour
    {
        [Header("Character Data")]
        public NetworkVariable<int> CharacterIndex = new NetworkVariable<int>(
            -1,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Owner
        );

        [Header("Synchronized Stats")]
        public NetworkVariable<float> NetworkHealth = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> NetworkAmmo = new NetworkVariable<int>(
            30,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Referencias aos sistemas locais (serao populadas no Start)
        // TODO: Adicionar referencias aos componentes do jogador quando existirem
        // private PlayerMovement movement;
        // private PlayerHealthSystem health;
        // private PlayerShooting shooting;
        // private PlayerCombatManager combat;
        // private CommanderAbilityController abilities;

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[NetworkedPlayerController] Spawned - IsOwner: {IsOwner}, IsServer: {IsServer}");

            // Buscar componentes
            // movement = GetComponent<PlayerMovement>();
            // health = GetComponent<PlayerHealthSystem>();
            // shooting = GetComponent<PlayerShooting>();
            // combat = GetComponent<PlayerCombatManager>();
            // abilities = GetComponent<CommanderAbilityController>();

            // Desabilitar input para jogadores que nao sao donos
            if (!IsOwner)
            {
                DisableLocalControls();
            }

            // Sincronizar dados iniciais
            if (IsServer)
            {
                InitializeServerData();
            }

            // Escutar mudancas de valores
            NetworkHealth.OnValueChanged += OnHealthChanged;
            NetworkAmmo.OnValueChanged += OnAmmoChanged;
            CharacterIndex.OnValueChanged += OnCharacterChanged;

            // Registrar no PlayerRegistry
            if (IsServer)
            {
                GameServer.PlayerRegistry.Instance?.RegisterPlayer(OwnerClientId, gameObject);
            }
        }

        private void InitializeServerData()
        {
            // TODO: Sincronizar valores iniciais dos sistemas locais
            // NetworkHealth.Value = health.currentHealth;
            // NetworkAmmo.Value = shooting.currentAmmo;
        }

        private void DisableLocalControls()
        {
            // Desabilitar controles para jogadores remotos
            // TODO: Desabilitar input quando componentes existirem
            // if (movement != null) movement.enabled = false;
            // if (shooting != null) shooting.enabled = false;
            // if (combat != null) combat.enabled = false;
            // if (abilities != null) abilities.enabled = false;

            Debug.Log($"[NetworkedPlayerController] Controles locais desabilitados (jogador remoto)");
        }

        // Callbacks de mudanca de valores
        private void OnHealthChanged(float oldValue, float newValue)
        {
            Debug.Log($"[NetworkedPlayerController] Vida mudou: {oldValue} -> {newValue}");
            // TODO: Atualizar sistema de vida local
            // if (health != null) health.currentHealth = newValue;
        }

        private void OnAmmoChanged(int oldValue, int newValue)
        {
            Debug.Log($"[NetworkedPlayerController] Municao mudou: {oldValue} -> {newValue}");
            // TODO: Atualizar sistema de municao local
            // if (shooting != null) shooting.currentAmmo = newValue;
        }

        private void OnCharacterChanged(int oldValue, int newValue)
        {
            Debug.Log($"[NetworkedPlayerController] Personagem mudou: {oldValue} -> {newValue}");
            // TODO: Carregar modelo do personagem
        }

        /// <summary>
        /// Receber dano (chamado via ServerRpc)
        /// </summary>
        [ServerRpc(RequireOwnership = false)]
        public void TakeDamageServerRpc(float damage, ulong attackerId)
        {
            if (!IsServer) return;

            // Servidor calcula dano
            float finalDamage = damage; // TODO: Aplicar resistencias
            NetworkHealth.Value = Mathf.Max(0, NetworkHealth.Value - finalDamage);

            Debug.Log($"[NetworkedPlayerController] Dano recebido: {damage}. Vida: {NetworkHealth.Value}");

            // Verificar morte
            if (NetworkHealth.Value <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            if (!IsServer) return;

            Debug.Log($"[NetworkedPlayerController] Jogador {OwnerClientId} morreu");

            // Notificar todos os clientes
            OnPlayerDiedClientRpc();

            // Respawnar apos delay
            Invoke(nameof(Respawn), 3f);
        }

        private void Respawn()
        {
            if (!IsServer) return;

            Debug.Log($"[NetworkedPlayerController] Jogador {OwnerClientId} respawnando");

            // Resetar vida
            NetworkHealth.Value = 100f; // TODO: Usar valor do CharacterData

            // Notificar clientes
            OnPlayerRespawnedClientRpc();

            // TODO: Teletransportar para spawn point
        }

        [ClientRpc]
        private void OnPlayerDiedClientRpc()
        {
            Debug.Log("[NetworkedPlayerController] Animacao de morte");
            // TODO: Reproduzir animacao de morte
            // TODO: Desabilitar controles temporariamente
        }

        [ClientRpc]
        private void OnPlayerRespawnedClientRpc()
        {
            Debug.Log("[NetworkedPlayerController] Respawn completo");
            // TODO: Reproduzir efeito de respawn
            // TODO: Reabilitar controles
        }

        /// <summary>
        /// Atualizar municao (chamado pelo sistema de tiro)
        /// </summary>
        [ServerRpc]
        public void UpdateAmmoServerRpc(int newAmmo)
        {
            if (!IsServer) return;
            NetworkAmmo.Value = newAmmo;
        }

        public override void OnNetworkDespawn()
        {
            // Remover listeners
            NetworkHealth.OnValueChanged -= OnHealthChanged;
            NetworkAmmo.OnValueChanged -= OnAmmoChanged;
            CharacterIndex.OnValueChanged -= OnCharacterChanged;

            // Remover do registro
            if (IsServer)
            {
                GameServer.PlayerRegistry.Instance?.UnregisterPlayer(OwnerClientId);
            }
        }
    }
}
