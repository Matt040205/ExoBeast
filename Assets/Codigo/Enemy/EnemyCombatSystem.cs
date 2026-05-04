using System.Collections;
using Unity.Netcode;
using UnityEngine;

public class EnemyCombatSystem : NetworkBehaviour
{
    private enum AttackState
    {
        Idle,
        Windup,
        Recover
    }

    [Header("Configuracoes de Combate")]
    public float attackRange = 2f;
    public float timeToDamage = 2f;

    [Header("Aura de Dano em Torres")]
    public float towerAuraRadius = 10f;
    public float towerAuraDamage = 14f;
    public float towerAuraInterval = 3f;

    [Header("Efeitos Visuais")]
    public GameObject attackVfxPrefab;

    [Header("Referencias")]
    public Transform attackPoint;
    public LayerMask playerLayer;
    public LayerMask towerLayer;

    private EnemyController enemyController;
    private EnemyDataSO enemyData;
    private float currentDamage;
    private Coroutine attackCoroutine;
    private Coroutine towerAuraCoroutine;
    private AttackState attackState = AttackState.Idle;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        enemyController = GetComponent<EnemyController>();
        if (!IsServer)
        {
            enabled = false;
            return;
        }
    }

    public void InitializeCombat(EnemyDataSO data, int nivel)
    {
        if (!IsServer)
            return;

        enemyData = data;
        if (enemyData == null)
            return;

        currentDamage = enemyData.GetDamage(nivel);

        if (towerAuraCoroutine != null)
            StopCoroutine(towerAuraCoroutine);

        towerAuraCoroutine = StartCoroutine(TowerAuraCycle());
        ResetAttackState();
    }

    private void Update()
    {
        if (!IsServer || enemyController == null || enemyController.IsDead || enemyData == null)
            return;

        Transform currentTarget = enemyController.Target;
        bool hasValidTarget = currentTarget != null && (currentTarget.CompareTag("Player") || currentTarget.GetComponent<TowerController>() != null || currentTarget.GetComponent<ExoBeasts.Multiplayer.Sync.NetworkedBuilding>() != null);

        if (!hasValidTarget)
        {
            ResetAttackState();
            return;
        }

        bool inRange = false;
        if (attackState != AttackState.Idle)
        {
            inRange = enemyController.IsTargetInDisengageRange(currentTarget);
        }
        else
        {
            inRange = enemyController.IsTargetInAttackRange(currentTarget);
        }

        if (!inRange)
        {
            ResetAttackState();
            return;
        }

        enemyController.HoldAttackPosition();

        if (attackCoroutine == null)
            attackCoroutine = StartCoroutine(AttackCycle(currentTarget));
    }

    private IEnumerator AttackCycle(Transform initialTarget)
    {
        Debug.Log($"[EnemyCombatSystem] Entrou no Range contra {initialTarget.name}. Iniciando ciclo de ataque.");
        Transform trackedTarget = initialTarget;

        while (CanContinueAttacking(trackedTarget))
        {
            Debug.Log("[EnemyCombatSystem] Cooldown Liberado. Disparando animacao de ataque.");
            attackState = AttackState.Windup;

            PlayAttackAnimationClientRpc();

            yield return new WaitForSeconds(timeToDamage);

            if (!CanContinueAttacking(trackedTarget))
            {
                Debug.Log("[EnemyCombatSystem] Alvo saiu do range ou morreu durante o Windup. Abortando aplicacao do dano.");
                break;
            }

            Debug.Log("[EnemyCombatSystem] Tempo de Windup (timeToDamage) concluido. Processando VFX e Dano.");
            TriggerAttackVfx(trackedTarget.position);
            ProcessAttack(trackedTarget);

            attackState = AttackState.Recover;
            float cooldown = enemyData.attackSpeed > 0f ? 1f / enemyData.attackSpeed : 1f;
            Debug.Log($"[EnemyCombatSystem] Entrando em cooldown de {cooldown} segundos.");
            
            yield return new WaitForSeconds(cooldown);

            trackedTarget = enemyController.Target;
        }

        Debug.Log("[EnemyCombatSystem] Ciclo de ataque quebrado. Alvo fora de alcance ou morto. Retomando patrulha/perseguição.");
        attackState = AttackState.Idle;
        attackCoroutine = null;
        
        if (enemyController != null)
            enemyController.ResumeMovement();
    }

    [ClientRpc]
    private void PlayAttackAnimationClientRpc()
    {
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("doAttack"); // Certifique-se de que o parâmetro na Unity se chama exatamente assim
        }
        else
        {
            Debug.LogWarning("[EnemyCombatSystem] Animator nao encontrado no inimigo para tocar animacao de ataque.");
        }
    }

    private bool CanContinueAttacking(Transform trackedTarget)
    {
        if (enemyController == null || enemyController.IsDead)
            return false;

        Transform currentTarget = enemyController.Target;
        if (currentTarget == null || currentTarget != trackedTarget)
            return false;

        bool isValid = currentTarget.CompareTag("Player") || currentTarget.GetComponent<TowerController>() != null || currentTarget.GetComponent<ExoBeasts.Multiplayer.Sync.NetworkedBuilding>() != null;
        if (!isValid)
            return false;

        return enemyController.IsTargetInDisengageRange(currentTarget);
    }

    private void TriggerAttackVfx(Vector3 targetPos)
    {
        if (attackVfxPrefab == null)
            return;

        Vector3 origin = attackPoint != null ? attackPoint.position : transform.position;
        Vector3 direction = (targetPos - origin).normalized;
        if (direction == Vector3.zero)
            direction = transform.forward;

        ExoBeasts.Multiplayer.Sync.NetworkedEnemy networkedEnemy = GetComponent<ExoBeasts.Multiplayer.Sync.NetworkedEnemy>();
        if (networkedEnemy != null && networkedEnemy.IsSpawned)
        {
            networkedEnemy.PlayAttackVfxClientRpc(origin, Quaternion.LookRotation(direction));
        }
        else
        {
            GlobalVFXPool.GetVFX(attackVfxPrefab, origin, Quaternion.LookRotation(direction), 2f);
        }
    }

    private void ProcessAttack(Transform targetTransform)
    {
        if (enemyController.IsBlinded && Random.value < 0.8f)
            return;

        // Garante dano no alvo principal travado ignorando físicas que poderiam falhar
        DealDamageToTarget(targetTransform);

        // Se for melee, também espalha o dano em área para quem estiver perto
        if (enemyData.enemyType != EnemyType.Voador)
        {
            ApplyDamageInArea(targetTransform);
        }
    }

    private void DealDamageToTarget(Transform targetTransform)
    {
        if (targetTransform == null)
        {
            Debug.LogWarning("[EnemyCombatSystem] Alvo nulo na hora do dano.");
            return;
        }
        
        PlayerHealthSystem playerHealth = targetTransform.GetComponent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            Debug.Log($"[EnemyCombatSystem] Dano Aplicado ao Player ({targetTransform.name}).");
            playerHealth.TakeDamage(currentDamage, transform, enemyData.enemyType != EnemyType.Voador);
        }
        else
        {
            TowerController tower = targetTransform.GetComponent<TowerController>();
            if (tower != null)
            {
                Debug.Log($"[EnemyCombatSystem] Dano Aplicado a Torre ({targetTransform.name}).");
                tower.TakeDamage(currentDamage);
            }
            else
            {
                Debug.LogWarning($"[EnemyCombatSystem] Alvo ({targetTransform.name}) nao possui componente de vida reconhecido. Dano ignorado.");
            }
        }
    }

    private void ApplyDamageInArea(Transform excludeTarget)
    {
        if (!IsServer || enemyData == null)
            return;

        Vector3 origin = attackPoint != null ? attackPoint.position : transform.position;
        Collider[] hitTargets = Physics.OverlapSphere(origin, attackRange, playerLayer | towerLayer);

        foreach (Collider col in hitTargets)
        {
            if (col.transform == excludeTarget) continue;

            PlayerHealthSystem playerHealth = col.GetComponent<PlayerHealthSystem>();
            if (playerHealth != null)
            {
                playerHealth.TakeDamage(currentDamage, transform, true);
            }
            else
            {
                TowerController tower = col.GetComponent<TowerController>();
                if (tower != null)
                    tower.TakeDamage(currentDamage);
            }
        }
    }

    private IEnumerator TowerAuraCycle()
    {
        yield return null;

        while (enemyController != null && !enemyController.IsDead)
        {
            ApplyAuraDamageToTowers();
            yield return new WaitForSeconds(towerAuraInterval);
        }
    }

    private void ApplyAuraDamageToTowers()
    {
        if (!IsServer)
            return;

        Collider[] hitTowers = Physics.OverlapSphere(transform.position, towerAuraRadius, towerLayer);
        foreach (Collider towerCollider in hitTowers)
        {
            TowerController tower = towerCollider.GetComponent<TowerController>();
            if (tower != null)
                tower.TakeDamage(towerAuraDamage);
        }
    }

    private void ResetAttackState()
    {
        if (attackCoroutine != null)
            StopCoroutine(attackCoroutine);

        attackCoroutine = null;
        attackState = AttackState.Idle;

        if (enemyController != null)
            enemyController.ResumeMovement();
    }

    private void OnDisable()
    {
        if (towerAuraCoroutine != null)
            StopCoroutine(towerAuraCoroutine);

        ResetAttackState();
    }

    private void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, towerAuraRadius);
    }
}
