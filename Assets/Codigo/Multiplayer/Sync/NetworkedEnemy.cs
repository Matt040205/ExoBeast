using UnityEngine;
using Unity.Netcode;

namespace ExoBeasts.Multiplayer.Sync
{
    /// <summary>
    /// ── NetworkedEnemy ───────────────────────────────────
    /// Classe base para todos os inimigos sincronizados em rede.
    ///
    ///  ▸ IA executada exclusivamente no servidor via RunAI() (protected virtual)
    ///  ▸ Vida e estado sincronizados via NetworkVariable (server-write)
    ///  ▸ Dano recebivel de qualquer cliente via TakeDamageServerRpc (RequireOwnership = false)
    ///  ▸ Morte: notifica NetworkedHorde, dispara ClientRpc, despawna apos 2s
    ///  ▸ Subclasses sobrescrevem: RunAI, OnHitClientRpc, OnDiedClientRpc
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class NetworkedEnemy : NetworkBehaviour
    {
        public enum EnemyState : int
        {
            Idle     = 0,
            Moving   = 1,
            Attacking = 2,
            Dead     = 3
        }

        [Header("Stats Base")]
        [SerializeField] protected float maxHealth = 100f;

        public NetworkVariable<float> CurrentHealth = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<int> State = new NetworkVariable<int>(
            (int)EnemyState.Idle,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        protected Transform currentTarget;
        protected bool isDead = false;

        public override void OnNetworkSpawn()
        {
            if (IsServer)
            {
                CurrentHealth.Value = maxHealth;
                State.Value = (int)EnemyState.Idle;
                Debug.Log($"[NetworkedEnemy] {gameObject.name} spawnado no servidor. " +
                          $"Vida: {maxHealth}");
            }

            State.OnValueChanged         += OnStateChanged;
            CurrentHealth.OnValueChanged += OnHealthChanged;
        }

        private void Update()
        {
            if (!IsServer || isDead) return;
            RunAI();
        }

        /// <summary>Logica de IA do inimigo. Chamado todo frame no servidor. Sobrescreva nas subclasses.</summary>
        protected virtual void RunAI()
        {
        }

        /// <summary>Aplica dano ao inimigo. RequireOwnership = false permite chamada de qualquer cliente.</summary>
        [ServerRpc(RequireOwnership = false)]
        public void TakeDamageServerRpc(float damage, ulong attackerClientId)
        {
            if (isDead || !IsServer) return;

            float finalDamage = Mathf.Max(0, damage);
            CurrentHealth.Value -= finalDamage;

            Debug.Log($"[NetworkedEnemy] {gameObject.name} recebeu {finalDamage} de dano " +
                      $"(atacante: {attackerClientId}). Vida: {CurrentHealth.Value}/{maxHealth}");

            OnHitClientRpc(finalDamage);

            if (CurrentHealth.Value <= 0)
            {
                Die();
            }
        }

        /// <summary>Processa a morte do inimigo no servidor.</summary>
        protected virtual void Die()
        {
            if (!IsServer || isDead) return;

            isDead = true;
            State.Value = (int)EnemyState.Dead;

            Debug.Log($"[NetworkedEnemy] {gameObject.name} foi eliminado.");

            if (NetworkedHorde.Instance != null)
            {
                NetworkedHorde.Instance.OnEnemyKilledServerRpc();
            }

            OnDiedClientRpc();

            var col = GetComponent<Collider>();
            if (col != null) col.enabled = false;

            Invoke(nameof(DespawnEnemy), 2f);
        }

        private void DespawnEnemy()
        {
            if (!IsServer) return;

            var networkObject = GetComponent<NetworkObject>();
            if (networkObject != null && networkObject.IsSpawned)
            {
                networkObject.Despawn(destroy: true);
            }
        }

        [ClientRpc]
        protected virtual void OnHitClientRpc(float damage)
        {
        }

        [ClientRpc]
        protected virtual void OnDiedClientRpc()
        {
        }

        protected virtual void OnStateChanged(int oldState, int newState)
        {
            Debug.Log($"[NetworkedEnemy] {gameObject.name}: {(EnemyState)oldState} → {(EnemyState)newState}");
        }

        protected virtual void OnHealthChanged(float oldHealth, float newHealth)
        {
        }

        /// <summary>Define o alvo da IA. Apenas servidor usa este metodo.</summary>
        public void SetTarget(Transform target)
        {
            if (!IsServer) return;
            currentTarget = target;
        }

        /// <summary>Retorna o estado atual como enum (legivel).</summary>
        public EnemyState GetCurrentState() => (EnemyState)State.Value;

        /// <summary>Retorna true se o inimigo esta morto.</summary>
        public bool IsDead() => isDead;

        public override void OnNetworkDespawn()
        {
            State.OnValueChanged         -= OnStateChanged;
            CurrentHealth.OnValueChanged -= OnHealthChanged;
        }
    }
}
