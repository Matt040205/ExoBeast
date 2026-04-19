using UnityEngine;
using UnityEngine.AI;
using Unity.Netcode;
using System.Collections;

namespace ExoBeasts.Multiplayer.Sync
{
    public class NetworkedEnemy : NetworkBehaviour
    {
        [Header("Estado de Rede")]
        public NetworkVariable<float> NetworkHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
        public NetworkVariable<bool> IsDead = new NetworkVariable<bool>(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

        private EnemyController enemyController;
        private NavMeshAgent navMeshAgent;
        private EnemyHealthSystem localHealth;

        public override void OnNetworkSpawn()
        {
            enemyController = GetComponent<EnemyController>();
            navMeshAgent = GetComponent<NavMeshAgent>();
            localHealth = GetComponent<EnemyHealthSystem>();

            if (enemyController != null) enemyController.enabled = IsServer;
            if (navMeshAgent != null) navMeshAgent.enabled = IsServer;

            IsDead.OnValueChanged += OnDeathStateChanged;
        }

        [ServerRpc(RequireOwnership = false)]
        public void TakeDamageServerRpc(float damage, float armorPen, bool isCrit, ServerRpcParams rpcParams = default)
        {
            if (IsDead.Value) return;

            // PASSO 3 - A MÁGICA: O `ServerRpcParams rpcParams = default` diz ao Netcode para preencher
            // automaticamente os metadados da mensagem na chegada ao Servidor.
            // Aqui ele captura exatamente de onde a requisição veio, resolvendo o bug do 'ID 0' (pois
            // o `SenderClientId` conterá o ID verdadeiro de quem disparou o ServerRpc na ponta cliente).
            ulong attackerId = rpcParams.Receive.SenderClientId;

            if (localHealth != null)
            {
                // Passamos o ID salvo do atacante para o próximo sistema, garantindo o rastreio
                localHealth.TakeDamage(damage, armorPen, isCrit, attackerId);
            }
        }

        public void TriggerHitVisual(float finalDamage, bool isCritical, ulong attackerId)
        {
            if (!IsServer) return;

            // Flash branco → todos os clientes veem
            ShowHitFlashClientRpc();

            // Aqui montamos o envio seletivo: a variável Send agrupa as regras do ClientRpc.
            // Passamos especificamente o `attackerId` que foi capturado via ServerRpcParams lá de cima.
            ClientRpcParams popupParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    // Se não for fornecido, ou ficar vazio, faria broadcast.
                    // Com o ID verdadeiro (ex: 2) invés do default '0', apenas o cliente '2' receberá isso.
                    TargetClientIds = new ulong[] { attackerId }
                }
            };
            
            // Disparamos o ClientRpc de volta para a UI, embutindo as ClientRpcParams exclusivas para o alvo
            ShowDamagePopupClientRpc(finalDamage, isCritical, popupParams);
        }

        [ClientRpc]
        private void ShowHitFlashClientRpc()
        {
            // Flash aparece para todo mundo, sem popup
            if (localHealth != null)
                localHealth.ShowHitVisualLocal(0f, false, showPopup: false);
        }

        [ClientRpc]
        private void ShowDamagePopupClientRpc(float damageAmount, bool isCritical,
                                       ClientRpcParams clientRpcParams = default)
        {
            // Se você recebeu este RPC, você É o atacante — mostra o popup sem comparação
            if (localHealth != null)
                localHealth.ShowHitVisualLocal(damageAmount, isCritical, showPopup: true);
        }

        public IEnumerator DieRoutine()
        {
            if (IsDead.Value) yield break;
            IsDead.Value = true;

            if (HordeManager.Instance != null)
            {
                HordeManager.Instance.OnEnemyKilledServerRpc();
            }

            OnEnemyDiedClientRpc();

            yield return new WaitForSeconds(2f);

            var netObj = GetComponent<NetworkObject>();
            if (netObj != null && netObj.IsSpawned)
                netObj.Despawn(false);
        }

        [ClientRpc]
        private void OnEnemyDiedClientRpc()
        {
            if (enemyController != null) enemyController.enabled = false;
            if (navMeshAgent != null) navMeshAgent.enabled = false;

            var anim = GetComponentInChildren<Animator>();
            if (anim != null)
            {
                anim.SetBool("isWalking", false);
                anim.SetTrigger("isDead");
            }

            // Desova do efeito visual de morte (se configurado) independente do script do inimigo
            if (localHealth != null && localHealth.deathVfxPrefab != null)
            {
                var effect = Instantiate(localHealth.deathVfxPrefab, transform.position, transform.rotation);
                Destroy(effect, 4f);
            }
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

        [ClientRpc]
        public void PlayAttackVfxClientRpc(Vector3 position, Quaternion rotation)
        {
            var combatInfo = GetComponent<EnemyCombatSystem>();
            if (combatInfo != null && combatInfo.attackVfxPrefab != null)
            {
                var flash = Instantiate(combatInfo.attackVfxPrefab, position, rotation);
                Destroy(flash, 2f);
            }
        }
    }
}