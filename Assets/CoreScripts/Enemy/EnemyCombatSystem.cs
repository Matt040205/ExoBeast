using System.Collections;
using Unity.Netcode;
using UnityEngine;
using FMODUnity;

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
    [Header("Nota: O tempo do dano agora é controlado pelo Animation Event")]
    // public float timeToDamage = 2f; -> Removido para delegar a responsabilidade ao Animator.

    [Header("Aura de Dano em Torres")]
    public float towerAuraRadius = 10f;
    public float towerAuraDamage = 14f;
    public float towerAuraInterval = 3f;

    [Header("Efeitos Visuais")]
    public GameObject attackVfxPrefab;

    [Header("FMOD - Sons")]
    [EventRef] public string eventoAtaque;

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

    // Salva o alvo atual para o Animation Event saber quem golpear
    private Transform targetForAnimationEvent;
    
    private float nextAttackTime = 0f;
    private bool hasDealtDamageThisCycle = false;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        enemyController = GetComponent<EnemyController>();

        if (IsServer)
        {
            enabled = true;
        }
        else
        {
            enabled = false;
        }
    }

    public void InitializeCombat(EnemyDataSO data, int nivel)
    {
        if (enemyController == null)
            enemyController = GetComponent<EnemyController>();

        if (!enabled)
            enabled = true;

        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            enabled = false;
            return;
        }

        enemyData = data;
        if (enemyData == null)
            return;

        currentDamage = enemyData.GetDamage(nivel);
        ResetAttackState();
    }

    private void Update()
    {
        if (enemyController == null)
            enemyController = GetComponent<EnemyController>();

        if (enemyController == null || enemyController.IsDead || enemyData == null)
            return;

        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            return;

        Transform currentTarget = enemyController.Target;

        if (currentTarget == null)
        {
            ResetAttackState();
            return;
        }

        if (attackCoroutine != null)
        {
            return;
        }

        if (!IsValidCombatTarget(currentTarget))
            return;

        if (!enemyController.IsTargetInAttackRange(currentTarget))
            return;

        if (Time.time < nextAttackTime)
        {
            return;
        }

        enemyController.HoldAttackPosition();
        attackCoroutine = StartCoroutine(AttackCycle(currentTarget));
    }

    private IEnumerator AttackCycle(Transform initialTarget)
    {
        attackState = AttackState.Windup;

        // Salva o alvo para que o AnimationEvent saiba em quem bater quando o frame correto chegar
        targetForAnimationEvent = initialTarget;
        hasDealtDamageThisCycle = false;

        PlayAttackAnimationClientRpc();

        // Calcula o tempo de recarga com base na velocidade de ataque
        float cooldown = enemyData.attackSpeed > 0f ? 1f / enemyData.attackSpeed : 1f;
        nextAttackTime = Time.time + cooldown;

        // O dano ocorrerá via AnimationEvent_ApplyDamage().
        // Em vez de pausar a caminhada por todo o cooldown, pausamos por no máximo 0.5 segundos (tempo do golpe)
        // e liberamos o inimigo para continuar andando em direção à base enquanto recarrega.
        float pauseTime = Mathf.Min(0.5f, cooldown);
        yield return new WaitForSeconds(pauseTime);

        if (enemyController != null)
            enemyController.ResumeMovement();

        float remainingCooldown = cooldown - pauseTime;
        if (remainingCooldown > 0f)
        {
            yield return new WaitForSeconds(remainingCooldown);
        }

        attackState = AttackState.Idle;
        attackCoroutine = null;
    }

    // =======================================================================
    // MÉTODO CHAMADO PELO ANIMATION EVENT NO FRAME EXATO DO GOLPE
    // =======================================================================
    public void AnimationEvent_ApplyDamage()
    {
        // Garante que apenas o servidor aplica o dano de verdade na rede
        if (!IsServer) return;

        if (hasDealtDamageThisCycle) return;

        if (enemyController == null || enemyController.IsDead || targetForAnimationEvent == null)
            return;

        hasDealtDamageThisCycle = true;

        // Se o inimigo morre ou for atordoado no meio da animação, você pode colocar checks aqui

        TriggerAttackVfx(targetForAnimationEvent.position);
        ProcessAttack(targetForAnimationEvent);

        attackState = AttackState.Recover;
    }
    // =======================================================================

    [ClientRpc]
    private void PlayAttackAnimationClientRpc()
    {
        Animator animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            animator.SetTrigger("doAttack");
        }
        else
        {
            Debug.LogWarning("[EnemyCombatSystem] Animator nao encontrado no inimigo para tocar animacao de ataque.");
        }

        if (!string.IsNullOrEmpty(eventoAtaque))
        {
            RuntimeManager.PlayOneShot(eventoAtaque, transform.position);
        }
    }

    private bool CanContinueAttacking(Transform trackedTarget)
    {
        if (enemyController == null || enemyController.IsDead)
            return false;

        Transform currentTarget = enemyController.Target;
        if (currentTarget == null)
            return false;

        GameObject currentRoot = currentTarget.root.gameObject;
        GameObject trackedRoot = trackedTarget.root.gameObject;
        if (currentRoot != trackedRoot)
            return false;

        if (!IsValidCombatTarget(currentTarget))
            return false;

        return enemyController.IsTargetInAttackRange(currentTarget);
    }

    private bool IsValidCombatTarget(Transform t)
    {
        if (t == null) return false;

        PlayerHealthSystem playerHealth = t.GetComponentInParent<PlayerHealthSystem>()
            ?? t.GetComponentInChildren<PlayerHealthSystem>();
        if (playerHealth != null)
            return playerHealth.currentHealth.Value > 0f;

        TowerController tower = t.GetComponentInParent<TowerController>()
            ?? t.GetComponentInChildren<TowerController>();
        if (tower != null)
            return !tower.IsDestroyed;

        if (t.GetComponentInParent<ExoBeasts.Multiplayer.Sync.NetworkedBuilding>() != null) return true;

        return false;
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

        SpiderRangedAttack rangedAttack = GetComponent<SpiderRangedAttack>();
        if (rangedAttack != null)
        {
            rangedAttack.FireProjectile(targetTransform, currentDamage);
        }
        else
        {
            DealDamageToTarget(targetTransform);

            EnemyBleedAttack bleed = GetComponent<EnemyBleedAttack>();
            if (bleed != null)
                bleed.ApplyBleed(targetTransform);

            if (enemyData.enemyType != EnemyType.Voador)
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
        if (playerHealth == null) playerHealth = targetTransform.GetComponentInParent<PlayerHealthSystem>();
        if (playerHealth == null) playerHealth = targetTransform.GetComponentInChildren<PlayerHealthSystem>();

        if (playerHealth != null)
        {
            Debug.Log($"[EnemyCombatSystem] Dano Aplicado ao Player ({playerHealth.gameObject.name}). Dano={currentDamage}");
            playerHealth.TakeDamage(currentDamage, transform, enemyData.enemyType != EnemyType.Voador);
            return;
        }

        TowerController tower = targetTransform.GetComponent<TowerController>();
        if (tower == null) tower = targetTransform.GetComponentInParent<TowerController>();
        if (tower == null) tower = targetTransform.GetComponentInChildren<TowerController>();

        if (tower != null)
        {
            Debug.Log($"[EnemyCombatSystem] Dano Aplicado a Torre ({tower.gameObject.name}). Dano={currentDamage}");
            tower.TakeDamage(currentDamage);
            return;
        }

        Debug.LogWarning($"[EnemyCombatSystem] Alvo '{targetTransform.name}' (root='{targetTransform.root.name}') nao possui PlayerHealthSystem nem TowerController em toda a hierarquia. Dano ignorado.");
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

    // Aura de dano removida conforme solicitação.

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