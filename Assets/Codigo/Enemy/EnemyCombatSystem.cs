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

    [Header("Configuracoes de Combate (Player)")]
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
        bool hasValidPlayerTarget = currentTarget != null && currentTarget.CompareTag("Player");

        if (!hasValidPlayerTarget || !enemyController.IsTargetInAttackRange(currentTarget))
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
        Transform trackedTarget = initialTarget;

        while (CanContinueAttacking(trackedTarget))
        {
            attackState = AttackState.Windup;

            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null)
                animator.SetTrigger("doAttack");

            yield return new WaitForSeconds(timeToDamage);

            if (!CanContinueAttacking(trackedTarget))
                break;

            TriggerAttackVfx(trackedTarget.position);
            ProcessAttack(trackedTarget);

            attackState = AttackState.Recover;
            float cooldown = enemyData.attackSpeed > 0f ? 1f / enemyData.attackSpeed : 1f;
            yield return new WaitForSeconds(cooldown);

            trackedTarget = enemyController.Target;
        }

        attackState = AttackState.Idle;
        attackCoroutine = null;
        enemyController.ResumeMovement();
    }

    private bool CanContinueAttacking(Transform trackedTarget)
    {
        if (enemyController == null || enemyController.IsDead)
            return false;

        Transform currentTarget = enemyController.Target;
        if (currentTarget == null || currentTarget != trackedTarget)
            return false;

        if (!currentTarget.CompareTag("Player"))
            return false;

        return enemyController.IsTargetInAttackRange(currentTarget);
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
        if (enemyData.enemyType == EnemyType.Voador)
        {
            Vector3 origin = attackPoint != null ? attackPoint.position : transform.position;
            Vector3 direction = (targetTransform.position - origin).normalized;

            if (Physics.Raycast(origin, direction, out RaycastHit hit, enemyController.loseSightDistance, playerLayer))
            {
                PlayerHealthSystem playerHealth = hit.collider.GetComponent<PlayerHealthSystem>();
                if (playerHealth != null)
                    playerHealth.TakeDamage(currentDamage, transform, false);
            }

            return;
        }

        ApplyDamageInArea();
    }

    private void ApplyDamageInArea()
    {
        if (!IsServer || enemyData == null)
            return;

        if (enemyController.IsBlinded && Random.value < 0.8f)
            return;

        Vector3 origin = attackPoint != null ? attackPoint.position : transform.position;
        Collider[] hitPlayers = Physics.OverlapSphere(origin, attackRange, playerLayer);

        foreach (Collider col in hitPlayers)
        {
            PlayerHealthSystem playerHealth = col.GetComponent<PlayerHealthSystem>();
            if (playerHealth != null)
                playerHealth.TakeDamage(currentDamage, transform, true);
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
