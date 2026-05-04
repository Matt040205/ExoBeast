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

        if (IsServer)
        {
            // Re-habilita caso tenha sido desativado em uma vida anterior do pool
            enabled = true;
        }
        else
        {
            enabled = false;
        }
    }

    public void InitializeCombat(EnemyDataSO data, int nivel)
    {
        // Garante que a referência ao controller existe (essencial para inimigos reciclados do pool)
        if (enemyController == null)
            enemyController = GetComponent<EnemyController>();

        // Re-habilita o componente caso tenha sido desativado em um ciclo anterior
        if (!enabled)
            enabled = true;

        // Em modo rede, apenas o servidor processa combate
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
        {
            enabled = false;
            return;
        }

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

        // Se já tem uma coroutine rodando, não interfere —
        // a coroutine gerencia o próprio ciclo via CanContinueAttacking.
        if (attackCoroutine != null)
        {
            enemyController.HoldAttackPosition();
            return;
        }

        if (!IsValidCombatTarget(currentTarget))
            return;

        // Só verifica range para INICIAR um novo ciclo
        if (!enemyController.IsTargetInAttackRange(currentTarget))
            return;

        enemyController.HoldAttackPosition();
        attackCoroutine = StartCoroutine(AttackCycle(currentTarget));
    }

    private IEnumerator AttackCycle(Transform initialTarget)
    {
        Transform trackedTarget = initialTarget;

        while (CanContinueAttacking(trackedTarget))
        {
            attackState = AttackState.Windup;

            PlayAttackAnimationClientRpc();

            yield return new WaitForSeconds(timeToDamage);

            // Após o windup, só aborta se o inimigo MORREU.
            // Se o alvo saiu do range, o golpe sai mesmo assim (commit to attack).
            if (enemyController == null || enemyController.IsDead)
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
        if (currentTarget == null)
            return false;

        // Compara o GameObject raiz para não quebrar se o target foi refreshado
        // para o mesmo jogador (ex: IA tick atualizou a referência)
        GameObject currentRoot = currentTarget.root.gameObject;
        GameObject trackedRoot = trackedTarget.root.gameObject;
        if (currentRoot != trackedRoot)
            return false;

        if (!IsValidCombatTarget(currentTarget))
            return false;

        // CORREÇÃO: usa IsTargetInAttackRange (não disengageRange que era 3u enquanto
        // o ataque iniciava a 16u — causava o while sair imediatamente todo frame).
        // O disengageDistance é para o EnemyController decidir quando PARAR de perseguir,
        // não para o CombatSystem decidir quando continuar atacando.
        return enemyController.IsTargetInAttackRange(currentTarget);
    }

    /// <summary>
    /// Verifica se um Transform é um alvo válido de combate,
    /// subindo e descendo na hierarquia (essencial para CharacterController).
    /// Rejeita alvos destruídos/mortos mesmo que o objeto ainda exista na cena (pooling).
    /// </summary>
    private bool IsValidCombatTarget(Transform t)
    {
        if (t == null) return false;

        // Checa jogador: aceita apenas se ainda tem vida
        PlayerHealthSystem playerHealth = t.GetComponentInParent<PlayerHealthSystem>()
            ?? t.GetComponentInChildren<PlayerHealthSystem>();
        if (playerHealth != null)
            return playerHealth.currentHealth.Value > 0f;

        // Checa torre: rejeita se já foi destruída (IsDestroyed = true)
        TowerController tower = t.GetComponentInParent<TowerController>()
            ?? t.GetComponentInChildren<TowerController>();
        if (tower != null)
            return !tower.IsDestroyed;

        // Checa NetworkedBuilding (sem estado de morte próprio — aceita se existir)
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

        // Dano imediato do golpe
        DealDamageToTarget(targetTransform);

        // Sangramento: aplicado se o inimigo tiver o componente EnemyBleedAttack
        EnemyBleedAttack bleed = GetComponent<EnemyBleedAttack>();
        if (bleed != null)
            bleed.ApplyBleed(targetTransform);

        // Se for melee, também espalha o dano em área para quem estiver perto
        if (enemyData.enemyType != EnemyType.Voador)
            ApplyDamageInArea(targetTransform);
    }

    private void DealDamageToTarget(Transform targetTransform)
    {
        if (targetTransform == null)
        {
            Debug.LogWarning("[EnemyCombatSystem] Alvo nulo na hora do dano.");
            return;
        }

        // CharacterController: PlayerHealthSystem pode estar no pai, no próprio objeto ou num filho.
        // Sempre busca em toda a hierarquia para não depender da estrutura exata do prefab.
        PlayerHealthSystem playerHealth = targetTransform.GetComponent<PlayerHealthSystem>();
        if (playerHealth == null) playerHealth = targetTransform.GetComponentInParent<PlayerHealthSystem>();
        if (playerHealth == null) playerHealth = targetTransform.GetComponentInChildren<PlayerHealthSystem>();

        if (playerHealth != null)
        {
            Debug.Log($"[EnemyCombatSystem] Dano Aplicado ao Player ({playerHealth.gameObject.name}). Dano={currentDamage}");
            playerHealth.TakeDamage(currentDamage, transform, enemyData.enemyType != EnemyType.Voador);
            return;
        }

        // Checa torre
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
