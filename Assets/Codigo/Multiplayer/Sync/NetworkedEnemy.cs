using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

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
            ulong attackerId = rpcParams.Receive.SenderClientId;
            PlayerHealthSystem attackerHealth = NetworkGameplayResolver.ResolvePlayerHealth(attackerId);
            DamageContext damageContext = new DamageContext(attackerId, isCrit, DamageFeedbackMode.InstigatorOnly);
            ApplyDamageServer(damage, armorPen, damageContext, attackerHealth, out _);
        }

        public bool ApplyDamageServer(float damage, float armorPen, bool isCrit, ulong attackerId, out float finalDamage)
        {
            DamageContext damageContext = new DamageContext(attackerId, isCrit, DamageFeedbackMode.InstigatorOnly);
            return ApplyDamageServer(damage, armorPen, damageContext, null, out finalDamage);
        }

        public bool ApplyDamageServer(
            float damage,
            float armorPen,
            DamageContext damageContext,
            PlayerHealthSystem attackerHealth,
            out float finalDamage)
        {
            finalDamage = 0f;

            if (!IsServer || IsDead.Value || localHealth == null)
                return false;

            localHealth.ApplyAuthoritativeDamageDetailed(
                damage,
                armorPen,
                damageContext,
                attackerHealth,
                out finalDamage);
            return finalDamage > 0f;
        }

        public void TriggerHitVisual(float finalDamage, DamageContext damageContext)
        {
            if (!IsServer) return;

            ShowHitFlashClientRpc();

            if (damageContext.FeedbackMode == DamageFeedbackMode.AllObservers)
            {
                ShowDamagePopupClientRpc(finalDamage, damageContext.IsCritical);
                return;
            }

            ClientRpcParams popupParams = new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new ulong[] { damageContext.AttackerClientId }
                }
            };

            ShowDamagePopupClientRpc(finalDamage, damageContext.IsCritical, popupParams);
        }

        [ClientRpc]
        private void ShowHitFlashClientRpc()
        {
            if (localHealth != null)
                localHealth.ShowHitVisualLocal(0f, false, showPopup: false);
        }

        [ClientRpc]
        private void ShowDamagePopupClientRpc(float damageAmount, bool isCritical, ClientRpcParams clientRpcParams = default)
        {
            if (localHealth != null)
                localHealth.ShowHitVisualLocal(damageAmount, isCritical, showPopup: true);
        }

        public IEnumerator DieRoutine()
        {
            if (IsDead.Value) yield break;
            IsDead.Value = true;

            if (HordeManager.Instance != null)
                HordeManager.Instance.OnEnemyKilledServerRpc();

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

            if (localHealth != null && localHealth.deathVfxPrefab != null)
                GlobalVFXPool.GetVFX(localHealth.deathVfxPrefab, transform.position, transform.rotation, 4f);
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
                GlobalVFXPool.GetVFX(combatInfo.attackVfxPrefab, position, rotation, 2f);
        }
    }
}
