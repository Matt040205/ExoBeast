using UnityEngine;
using Unity.Netcode;

namespace ExoBeasts.Multiplayer.Sync
{
    /// <summary>
    /// ── NetworkedPlayerController ────────────────────────
    /// Sincroniza dados vitais do jogador (vida, municao, personagem) em rede.
    ///
    ///  ▸ NetworkVariables: Health (server-write), Ammo e Character (owner/server)
    ///  ▸ TakeDamageServerRpc: dano de qualquer cliente, validacao e morte no servidor
    ///  ▸ Respawn automatico apos 3s via Invoke
    ///  ▸ Registra no PlayerRegistry ao spawnar; remove ao despawnar
    /// ─────────────────────────────────────────────────────
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

        public override void OnNetworkSpawn()
        {
            Debug.Log($"[NetworkedPlayerController] Spawned - IsOwner: {IsOwner}, IsServer: {IsServer}");

            if (!IsOwner)
            {
                DisableLocalControls();
            }

            if (IsServer)
            {
                InitializeServerData();
            }

            NetworkHealth.OnValueChanged += OnHealthChanged;
            NetworkAmmo.OnValueChanged += OnAmmoChanged;
            CharacterIndex.OnValueChanged += OnCharacterChanged;

            if (IsServer)
            {
                GameServer.PlayerRegistry.Instance?.RegisterPlayer(OwnerClientId, gameObject);
            }
        }

        private void InitializeServerData()
        {
        }

        private void DisableLocalControls()
        {
            Debug.Log($"[NetworkedPlayerController] Controles locais desabilitados (jogador remoto)");
        }

        private void OnHealthChanged(float oldValue, float newValue)
        {
            Debug.Log($"[NetworkedPlayerController] Vida mudou: {oldValue} -> {newValue}");
        }

        private void OnAmmoChanged(int oldValue, int newValue)
        {
            Debug.Log($"[NetworkedPlayerController] Municao mudou: {oldValue} -> {newValue}");
        }

        private void OnCharacterChanged(int oldValue, int newValue)
        {
            Debug.Log($"[NetworkedPlayerController] Personagem mudou: {oldValue} -> {newValue}");
        }

        [ServerRpc(RequireOwnership = false)]
        public void TakeDamageServerRpc(float damage, ulong attackerId)
        {
            if (!IsServer) return;

            float finalDamage = damage;
            NetworkHealth.Value = Mathf.Max(0, NetworkHealth.Value - finalDamage);

            Debug.Log($"[NetworkedPlayerController] Dano recebido: {damage}. Vida: {NetworkHealth.Value}");

            if (NetworkHealth.Value <= 0)
            {
                Die();
            }
        }

        private void Die()
        {
            if (!IsServer) return;

            Debug.Log($"[NetworkedPlayerController] Jogador {OwnerClientId} morreu");
            OnPlayerDiedClientRpc();
            Invoke(nameof(Respawn), 3f);
        }

        private void Respawn()
        {
            if (!IsServer) return;

            Debug.Log($"[NetworkedPlayerController] Jogador {OwnerClientId} respawnando");
            NetworkHealth.Value = 100f;
            OnPlayerRespawnedClientRpc();
        }

        [ClientRpc]
        private void OnPlayerDiedClientRpc()
        {
            Debug.Log("[NetworkedPlayerController] Animacao de morte");
        }

        [ClientRpc]
        private void OnPlayerRespawnedClientRpc()
        {
            Debug.Log("[NetworkedPlayerController] Respawn completo");
        }

        [ServerRpc]
        public void UpdateAmmoServerRpc(int newAmmo)
        {
            if (!IsServer) return;
            NetworkAmmo.Value = newAmmo;
        }

        public override void OnNetworkDespawn()
        {
            NetworkHealth.OnValueChanged -= OnHealthChanged;
            NetworkAmmo.OnValueChanged -= OnAmmoChanged;
            CharacterIndex.OnValueChanged -= OnCharacterChanged;

            if (IsServer)
            {
                GameServer.PlayerRegistry.Instance?.UnregisterPlayer(OwnerClientId);
            }
        }
    }
}
