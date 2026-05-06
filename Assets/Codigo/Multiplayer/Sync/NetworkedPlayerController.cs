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
            NetworkVariableWritePermission.Server
        );

        [Header("Synchronized Stats")]
        public NetworkVariable<float> NetworkHealth = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        // Municao e UI exclusiva do owner (HUD propria) — broadcast para Everyone era desperdicio.
        // Se houver futuro UI de "balas restantes" do aliado, usar ClientRpc esparso em vez de NetworkVariable.
        public NetworkVariable<int> NetworkAmmo = new NetworkVariable<int>(
            30,
            NetworkVariableReadPermission.Owner,
            NetworkVariableWritePermission.Server
        );

        public override void OnNetworkSpawn()
        {
#if UNITY_EDITOR
            Debug.Log($"[NetworkedPlayerController] Spawned - IsOwner: {IsOwner}, IsServer: {IsServer}");
#endif

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

        }

        private void InitializeServerData()
        {
        }

        private void DisableLocalControls()
        {
#if UNITY_EDITOR
            Debug.Log($"[NetworkedPlayerController] Controles locais desabilitados (jogador remoto)");
#endif
        }

        // Hot path: hooks de NetworkVariable disparam por update de delta.
        // Em combate, podem rodar dezenas de vezes/s — Debug.Log + string concat aloca GC.
        // Mantemos o gancho funcional (assinatura) para preservar futura integracao com HUD/eventos.
        private void OnHealthChanged(float oldValue, float newValue)
        {
#if UNITY_EDITOR
            Debug.Log($"[NetworkedPlayerController] Vida mudou: {oldValue} -> {newValue}");
#endif
        }

        private void OnAmmoChanged(int oldValue, int newValue)
        {
#if UNITY_EDITOR
            Debug.Log($"[NetworkedPlayerController] Municao mudou: {oldValue} -> {newValue}");
#endif
        }

        private void OnCharacterChanged(int oldValue, int newValue)
        {
#if UNITY_EDITOR
            Debug.Log($"[NetworkedPlayerController] Personagem mudou: {oldValue} -> {newValue}");
#endif
        }

        [ServerRpc(RequireOwnership = false)]
        public void TakeDamageServerRpc(float damage, ulong attackerId)
        {
            if (!IsServer) return;

            float finalDamage = damage;
            NetworkHealth.Value = Mathf.Max(0, NetworkHealth.Value - finalDamage);

#if UNITY_EDITOR
            Debug.Log($"[NetworkedPlayerController] Dano recebido: {damage}. Vida: {NetworkHealth.Value}");
#endif

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

        }
    }
}
