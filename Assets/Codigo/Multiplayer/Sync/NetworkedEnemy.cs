using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using System.Collections;

namespace ExoBeasts.Multiplayer.Sync
{
    /// <summary>
    /// Wrapper de rede para o inimigo.
    /// Apenas o servidor roda EnemyController e NavMeshAgent.
    /// Clientes recebem posicao via NetworkTransform e exibem o estado via NetworkVariables.
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

        public override void OnNetworkSpawn()
        {
            enemyController = GetComponent<EnemyController>();
            navMeshAgent = GetComponent<NavMeshAgent>();

            bool runAI = IsServer;
            if (enemyController != null) enemyController.enabled = runAI;
            if (navMeshAgent != null) navMeshAgent.enabled = runAI;

            IsDead.OnValueChanged += OnDeathStateChanged;
        }

        [ServerRpc(RequireOwnership = false)]
        public void TakeDamageServerRpc(float damage)
        {
            if (IsDead.Value) return;

            NetworkHealth.Value = Mathf.Max(0f, NetworkHealth.Value - damage);

            if (NetworkHealth.Value <= 0f)
                StartCoroutine(DieRoutine());
        }

        private IEnumerator DieRoutine()
        {
            IsDead.Value = true;
            NetworkedHorde.Instance?.OnEnemyKilledServerRpc();
            OnEnemyDiedClientRpc();

            yield return new WaitForSeconds(2f);

            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
                netObj.Despawn();
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
        }
    }
}
