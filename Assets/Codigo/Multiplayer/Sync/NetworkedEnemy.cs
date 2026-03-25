using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using System.Collections;

namespace ExoBeasts.Multiplayer.Sync
{
    /// <summary>
    /// ── NetworkedEnemy ──────────────────────────────────────
    /// Wrapper de rede para o inimigo — servidor roda AI, clientes recebem estado via NGO.
    ///
    ///  ▸ TakeDamageServerRpc: delega para EnemyHealthSystem no servidor
    ///  ▸ DieRoutine (server): sinaliza HordeManager, dispara ClientRpc de morte, Despawn
    ///  ▸ OnDeathStateChanged: desativa AI nos clientes ao receber IsDead = true
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class NetworkedEnemy : NetworkBehaviour
    {
        [Header("Estado de Rede")]
        public NetworkVariable<float> NetworkHealth = new NetworkVariable<float>(
            100f,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(
            false,
            NetworkVariableReadPermission.Everyone,
            NetworkVariableWritePermission.Server
        );

        private EnemyController enemyController;
        private NavMeshAgent navMeshAgent;
        private EnemyHealthSystem localHealth;

        public override void OnNetworkSpawn()
        {
            enemyController = GetComponent<EnemyController>();
            navMeshAgent = GetComponent<NavMeshAgent>();
            localHealth = GetComponent<EnemyHealthSystem>();

            bool runAI = IsServer;
            if (enemyController != null) enemyController.enabled = runAI;
            if (navMeshAgent != null) navMeshAgent.enabled = runAI;

            IsDead.OnValueChanged += OnDeathStateChanged;
        }

        [ServerRpc(RequireOwnership = false)]
        public void TakeDamageServerRpc(float damage, float armorPen, bool isCrit)
        {
            if (IsDead.Value) return;

            // Delegar para o EnemyHealthSystem processar a lógica no servidor
            if (localHealth != null)
            {
                localHealth.TakeDamage(damage, armorPen, isCrit);
            }
            else
            {
                // Fallback redundante
                NetworkHealth.Value = Mathf.Max(0f, NetworkHealth.Value - damage);
                if (NetworkHealth.Value <= 0f) StartCoroutine(DieRoutine());
            }
        }

        // Chamado apenas pelo servidor quando o dano é validado no EnemyHealthSystem
        public void TriggerHitVisual(float finalDamage, bool isCritical)
        {
            if (!IsServer) return;
            ShowHitVisualClientRpc(finalDamage, isCritical);
        }

        [ClientRpc]
        private void ShowHitVisualClientRpc(float damageAmount, bool isCritical)
        {
            // Executado em TODOS os clientes
            if (localHealth != null)
            {
                // Mostra o flash visual e o popup de dano no cliente atual
                localHealth.ShowHitVisualLocal(damageAmount, isCritical);
            }
        }

        public IEnumerator DieRoutine()
        {
            if (IsDead.Value) yield break;

            IsDead.Value = true;

            // Notificar o HordeManager unificado para progressão de ondas
            if (HordeManager.Instance != null)
            {
                HordeManager.Instance.OnEnemyKilledServerRpc();
            }

            OnEnemyDiedClientRpc();

            yield return new WaitForSeconds(2f);

            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
                netObj.Despawn(false); // Retorna ao pool via handler
        }

        [ClientRpc]
        private void OnEnemyDiedClientRpc()
        {
            if (enemyController != null) enemyController.enabled = false;
            if (navMeshAgent != null) navMeshAgent.enabled = false;

            var anim = GetComponent<Animator>();
            if (anim != null) anim.SetBool("isWalking", false);
        }

        private void OnDeathStateChanged(bool oldVal, bool newVal)
        {
            if (newVal && !IsServer)
            {
                if (enemyController != null) enemyController.enabled = false;
                if (navMeshAgent != null) navMeshAgent.enabled = false;
            }
        }

        public override void OnNetworkDespawn()
        {
            IsDead.OnValueChanged -= OnDeathStateChanged;
            base.OnNetworkDespawn();
        }
    }
}
