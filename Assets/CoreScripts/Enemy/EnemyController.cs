using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ExoBeasts.Multiplayer.GameServer;
using ExoBeasts.Multiplayer.Sync;

public enum AITargetPriority
{
    Player,
    Objective
}

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    [Header("Inteligencia Artificial")]
    public AITargetPriority mainPriority = AITargetPriority.Player;
    public float findDistance = 15f;
    public float attackDistance = 2f;
    public float loseSightDistance = 25f;
    public float selfDefenseRadius = 5f;
    public float maxChaseTime = 10f;
    public float maxChaseDistance = 20f;

    [Header("Hysteresis (Tolerância de Combate)")]
    public float disengageDistance = 3.0f;

    [Tooltip("Objeto visual (ex: ícone de exclamação) que liga quando o inimigo foca no jogador")]
    public GameObject aggroIndicatorVisual;

    [Header("Configuracoes Fisicas")]
    public float originalMoveSpeed = 3.5f;

    [Header("Status")]
    public bool IsDead;

    public bool IsBlinded => statusController != null && statusController.IsBlinded;
    public Transform Target => target;

    private NavMeshAgent agent;
    private Animator anim;
    private EnemyHealthSystem healthSystem;
    private EnemyCombatSystem combatSystem;
    private EnemyStatusController statusController;
    private ExoBeasts.Multiplayer.Sync.NetworkedEnemy networkedEnemy;

    private Transform target;
    private Transform playerTransform;
    private List<Transform> patrolPoints;
    private int currentPointIndex;
    private Vector3 initialChasePosition;
    private int nivel;
    private int pathIndex;
    private bool hasTriggeredHalfway;
    private int paintStacks;
    private float paintStackResetTime;

    private Coroutine tauntCoroutine;
    private Coroutine aiTickCoroutine;
    private Vector3 lastDestinationSet = Vector3.positiveInfinity;
    private Vector3 originalScale;
    private Coroutine runModifierCoroutine;

    private Vector3 lastTickPosition;
    private int stuckTickCount;
    private readonly List<Transform> playerTargetCandidates = new List<Transform>(4);

    private const string TAG_POCA = "Poca";
    private bool targetPlayerNext = true;

    public EnemyDataSO enemyData { get; private set; }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        healthSystem = GetComponent<EnemyHealthSystem>();
        combatSystem = GetComponent<EnemyCombatSystem>();
        statusController = GetComponent<EnemyStatusController>();
        networkedEnemy = GetComponent<ExoBeasts.Multiplayer.Sync.NetworkedEnemy>();
        originalScale = transform.localScale;

        if (statusController == null)
            statusController = gameObject.AddComponent<EnemyStatusController>();

        statusController.Initialize(agent, anim);
    }

    public void InitializeEnemy(Transform initialTarget, List<Transform> points, EnemyDataSO data, int level, int assignedPathIndex = 0)
    {
        playerTransform = initialTarget;
        patrolPoints = points;
        enemyData = data;
        nivel = level;
        pathIndex = assignedPathIndex;
        hasTriggeredHalfway = false;
        currentPointIndex = 0;
        IsDead = false;
        HordeManager.RegisterEnemy(this);
        paintStacks = 0;
        paintStackResetTime = 0f;
        lastDestinationSet = Vector3.positiveInfinity;
        lastTickPosition = transform.position;
        stuckTickCount = 0;

        SetAggroVisual(false);

        EnemyEvents.OnEnemySpawned?.Invoke(pathIndex + 1);
        statusController.ResetState();
        ApplyRunModifierScale();

        if (healthSystem != null)
            healthSystem.InitializeHealth(level);

        if (combatSystem != null)
            combatSystem.InitializeCombat(data, level);

        if (agent != null)
        {
            agent.enabled = false;
            agent.enabled = true;
            agent.Warp(transform.position);
            agent.isStopped = false;
            agent.speed = originalMoveSpeed;
        }

        if (aiTickCoroutine != null)
            StopCoroutine(aiTickCoroutine);

        if (runModifierCoroutine != null)
            StopCoroutine(runModifierCoroutine);

        runModifierCoroutine = StartCoroutine(RunModifierRoutine());

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            aiTickCoroutine = StartCoroutine(AI_TickRoutine());
        }
    }

    private void ApplyRunModifierScale()
    {
        float scaleMultiplier = 1f;

        if (ModificacaoRunState.IsActive(ModificacaoGameplayEffect.AlvosMinusculos))
            scaleMultiplier *= ModificacaoRunState.GetValue(ModificacaoGameplayEffect.AlvosMinusculos, 0.5f);

        if (ModificacaoRunState.IsActive(ModificacaoGameplayEffect.Gigantismo))
            scaleMultiplier *= ModificacaoRunState.GetValue(ModificacaoGameplayEffect.Gigantismo, 3f);

        transform.localScale = originalScale * scaleMultiplier;
    }

    private IEnumerator RunModifierRoutine()
    {
        while (!IsDead)
        {
            if (healthSystem != null && ModificacaoRunState.IsActive(ModificacaoGameplayEffect.FrenesiMortal))
            {
                float drainPerSecond = ModificacaoRunState.GetSecondaryValue(ModificacaoGameplayEffect.FrenesiMortal, 1f);
                if (drainPerSecond > 0f)
                    healthSystem.ApplyAuthoritativeDamage(drainPerSecond, 0f, false, 0);
            }

            yield return new WaitForSeconds(1f);
        }

        runModifierCoroutine = null;
    }

    private void Update()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            return;

        if (IsDead)
            return;

        if (paintStacks > 0 && Time.time > paintStackResetTime)
            paintStacks = 0;

        if (statusController != null && !statusController.CanMove)
        {
            FaceTarget();
            if (anim != null) anim.SetBool("isWalking", false);
            return;
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            bool isWalking = !agent.isStopped && agent.velocity.sqrMagnitude > 0.1f;
            if (anim != null)
                anim.SetBool("isWalking", isWalking);
        }
    }

    private IEnumerator AI_TickRoutine()
    {
        while (!IsDead)
        {
            if (tauntCoroutine != null || (statusController != null && !statusController.CanMove))
            {
                yield return new WaitForSeconds(0.3f);
                continue;
            }            DecideTargetTick();

            ResumeMovement();
            PatrolTick();

            if (target != null)
            {
                FaceTarget();
            }   

            if (agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped)
            {
                float distanceMoved = Vector3.Distance(transform.position, lastTickPosition);
                if (distanceMoved < 0.1f)
                {
                    stuckTickCount++;
                    if (stuckTickCount >= 3)
                    {
                        HandleStuckState();
                        stuckTickCount = 0;
                    }
                }
                else
                {
                    stuckTickCount = 0;
                }
            }
            else
            {
                stuckTickCount = 0;
            }

            lastTickPosition = transform.position;

            yield return new WaitForSeconds(0.3f);
        }
    }

    private void HandleStuckState()
    {
        if (target == null)
        {
            if (patrolPoints != null && patrolPoints.Count > 0 && currentPointIndex < patrolPoints.Count - 1)
            {
                currentPointIndex++;
            }
        }
        else
        {
            if (agent != null && agent.isOnNavMesh)
            {
                Vector2 randomCircle = Random.insideUnitCircle * 2f;
                Vector3 offsetTarget = target.position + new Vector3(randomCircle.x, 0, randomCircle.y);
                agent.SetDestination(offsetTarget);
                lastDestinationSet = offsetTarget;
            }
        }
    }

    private static readonly Collider[] _targetingBuffer = new Collider[64];    
    
    private void DecideTargetTick()
    {
        float allowedRadius = (mainPriority == AITargetPriority.Player) ? findDistance : selfDefenseRadius;

        // 1. Encontra Player mais próximo
        Transform nearestPlayer = null;
        float nearestPlayerDist = float.MaxValue;
        PlayerRegistry.CollectValidPlayerTransforms(playerTargetCandidates);
        foreach (Transform player in playerTargetCandidates)
        {
            if (player == null || player.CompareTag(TAG_POCA))
                continue;

            float distance = GetDistanceToTarget(player);
            if (distance < nearestPlayerDist)
            {
                nearestPlayerDist = distance;
                nearestPlayer = player;
            }
        }

        // 2. Encontra Torre mais próxima
        Transform nearestTower = null;
        float nearestTowerDist = float.MaxValue;
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, allowedRadius, _targetingBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _targetingBuffer[i];
            if (col == null) continue;

            TowerController towerObj = col.GetComponent<TowerController>();
            if (towerObj != null && !towerObj.IsDestroyed)
            {
                float distance = GetDistanceToTarget(col.transform);
                if (distance < nearestTowerDist)
                {
                    nearestTowerDist = distance;
                    nearestTower = col.transform;
                }
                continue;
            }

            if (col.GetComponent<NetworkedBuilding>() != null)
            {
                float distance = GetDistanceToTarget(col.transform);
                if (distance < nearestTowerDist)
                {
                    nearestTowerDist = distance;
                    nearestTower = col.transform;
                }
            }
        }

        // 3. Aplica lógica alternada
        if (nearestPlayer != null && nearestPlayerDist <= allowedRadius && nearestTower != null && nearestTowerDist <= allowedRadius)
        {
            // Ambos estão no alcance, alterna!
            if (targetPlayerNext)
            {
                target = nearestPlayer;
            }
            else
            {
                target = nearestTower;
            }
            targetPlayerNext = !targetPlayerNext;
            SetAggroVisual(true);
        }
        else if (nearestPlayer != null && nearestPlayerDist <= allowedRadius)
        {
            target = nearestPlayer;
            SetAggroVisual(true);
        }
        else if (nearestTower != null && nearestTowerDist <= allowedRadius)
        {
            target = nearestTower;
            SetAggroVisual(true);
        }
        else
        {
            target = null;
            SetAggroVisual(false);
        }
    }

    private void PatrolTick()
    {
        if (patrolPoints == null || patrolPoints.Count == 0 || currentPointIndex >= patrolPoints.Count)
        {
            if (anim != null)
                anim.SetBool("isWalking", false);

            AttackObjectiveAndDie();
            return;
        }

        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            if (agent != null)
            {
                agent.enabled = false;
                agent.enabled = true;
                agent.Warp(transform.position);
            }
            return;
        }

        Transform waypoint = patrolPoints[currentPointIndex];
        if (waypoint == null)
        {
            currentPointIndex++;
            return;
        }

        if (!hasTriggeredHalfway && patrolPoints.Count > 0 && currentPointIndex >= patrolPoints.Count / 2)
        {
            hasTriggeredHalfway = true;
            EnemyEvents.OnEnemyHalfway?.Invoke(pathIndex + 1);
        }

        Vector3 flatPosition = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatWaypoint = new Vector3(waypoint.position.x, 0f, waypoint.position.z);
        float distanceToWaypoint = Vector3.Distance(flatPosition, flatWaypoint);

        if (distanceToWaypoint <= 1.2f)
        {
            currentPointIndex++;
            return;
        }

        MoveTowardsPositionTick(waypoint.position, isPatrol: true);
    }

    private void MoveTowardsPositionTick(Vector3 targetPosition, bool isPatrol = false)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        float speedMultiplier = statusController != null ? statusController.SpeedModifier : 1f;
        agent.speed = originalMoveSpeed * speedMultiplier;

        agent.stoppingDistance = isPatrol ? 0f : attackDistance;

        if (Vector3.Distance(targetPosition, lastDestinationSet) > 0.5f)
        {
            agent.SetDestination(targetPosition);
            lastDestinationSet = targetPosition;
        }
    }

    private void FaceTarget()
    {
        if (target == null)
            return;

        Vector3 direction = (target.position - transform.position).normalized;
        direction.y = 0f;

        if (direction == Vector3.zero)
            return;

        Quaternion targetRotation = Quaternion.LookRotation(direction);
        transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
    }

    public void ApplyTaunt(Transform tauntTarget, float duration)
    {
        if (IsDead)
            return;

        if (tauntCoroutine != null)
            StopCoroutine(tauntCoroutine);

        tauntCoroutine = StartCoroutine(TauntRoutine(tauntTarget, duration));
    }

    public bool IsTargetInAttackRange(Transform targetOverride = null)
    {
        Transform currentTarget = targetOverride != null ? targetOverride : target;
        if (currentTarget == null)
            return false;

        return GetDistanceToTarget(currentTarget) <= attackDistance;
    }

    public bool IsTargetInDisengageRange(Transform targetOverride = null)
    {
        Transform currentTarget = targetOverride != null ? targetOverride : target;
        if (currentTarget == null)
            return false;

        return GetDistanceToTarget(currentTarget) <= disengageDistance;
    }

    private float GetDistanceToTarget(Transform targetTransform)
    {
        Vector3 flatSelf = new Vector3(transform.position.x, 0f, transform.position.z);
        Vector3 flatTarget = new Vector3(targetTransform.position.x, 0f, targetTransform.position.z);
        return Vector3.Distance(flatSelf, flatTarget);
    }

    public void HoldAttackPosition()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.velocity = Vector3.zero;
        }

        if (anim != null)
            anim.SetBool("isWalking", false);

        FaceTarget();
    }

    public void ResumeMovement()
    {
        if (statusController != null && !statusController.CanMove)
            return;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = false;
    }

    public void HandleDeath()
    {
        if (IsDead)
            return;

        IsDead = true;
        HordeManager.UnregisterEnemy(this);

        if (aiTickCoroutine != null)
            StopCoroutine(aiTickCoroutine);

        if (anim != null)
        {
            anim.SetBool("isWalking", false);
            // Acionamento direto do trigger que criamos no BaseEnemyController
            anim.SetTrigger("isDead");
        }

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = true;

        if (HordeManager.Instance != null)
        {
            if (HordeManager.Instance.IsLocalMode)
                HordeManager.Instance.OnEnemyKilled();
            else
                HordeManager.Instance.OnEnemyKilledServerRpc();
        }

        StartCoroutine(ReturnToPoolAfterDelay(1.5f));
    }

    public void ApplySlow(float percentage, float duration)
    {
        statusController?.ApplySlow(percentage, duration);
    }

    public void ApplySlip()
    {
        statusController?.ApplySlip();
    }

    public void AddPaintStack()
    {
        paintStacks++;
        paintStackResetTime = Time.time + 5f;

        if (paintStacks >= 5)
        {
            statusController?.ApplyStun(2f);
            paintStacks = 0;
        }
    }

    public void ApplyKnockback(Vector3 direction, float force)
    {
        statusController?.ApplyKnockback(direction, force);
    }

    public void ApplyKnockUp(float duration, float force)
    {
        statusController?.ApplyKnockUp(duration, force);
    }

    public void AplicarDesaceleracao(float percentualReducao)
    {
        statusController?.SetPersistentSlow(Mathf.Clamp01(percentualReducao));
    }

    public void RemoverDesaceleracao()
    {
        statusController?.ClearSlow();
    }

    public void ApplyStun(float duration)
    {
        if (!IsDead)
            statusController?.ApplyStun(duration);
    }

    public void SetBlinded(bool isBlinded)
    {
        statusController?.SetBlinded(isBlinded);
    }

    public void SetPatrolPoints(List<Transform> points)
    {
        patrolPoints = points;
    }

    public void LoseTarget()
    {
        target = null;
        lastDestinationSet = Vector3.positiveInfinity;
        SetAggroVisual(false);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();
    }

    private void SetAggroVisual(bool isActive)
    {
        SetAggroVisualLocal(isActive);

        if (networkedEnemy != null && networkedEnemy.IsSpawned)
            networkedEnemy.SetAggroVisualClientRpc(isActive);
    }

    public void SetAggroVisualLocal(bool isActive)
    {
        if (aggroIndicatorVisual != null && aggroIndicatorVisual.activeSelf != isActive)
        {
            aggroIndicatorVisual.SetActive(isActive);
        }
    }

    public void RefreshTargetNow()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            return;

        target = null;
        lastDestinationSet = Vector3.positiveInfinity;
        DecideTargetTick();
    }

    private IEnumerator TauntRoutine(Transform tauntTarget, float duration)
    {
        float timer = 0f;
        while (timer < duration && tauntTarget != null && !IsDead)
        {
            target = tauntTarget;
            yield return null;
            timer += Time.deltaTime;
        }

        tauntCoroutine = null;
    }

    private void AttackObjectiveAndDie()
    {
        ObjectiveHealthSystem objective = ObjectiveHealthSystem.Instance;
        if (objective != null && enemyData != null)
            objective.TakeDamage(enemyData.damageToBase);

        EnemyEvents.OnEnemyReachedBase?.Invoke();
        HandleDeath();
    }

    private bool TryEvaluateRegisteredBuildTargets(float allowedRadius, ref Transform nearestEntity, ref float nearestDistance)
    {
        BuildManager buildManager = BuildManager.Instance;
        if (buildManager == null)
            return false;

        bool registryAvailable = false;

        IReadOnlyList<TowerController> towers = buildManager.GetActiveTowers();
        if (towers != null && towers.Count > 0)
        {
            registryAvailable = true;
            for (int i = 0; i < towers.Count; i++)
            {
                TowerController tower = towers[i];
                if (tower == null || tower.IsDestroyed)
                    continue;

                float distance = GetDistanceToTarget(tower.transform);
                if (distance > allowedRadius || distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearestEntity = tower.transform;
            }
        }

        IReadOnlyList<NetworkedBuilding> buildings = buildManager.GetActiveBuildings();
        if (buildings != null && buildings.Count > 0)
        {
            registryAvailable = true;
            for (int i = 0; i < buildings.Count; i++)
            {
                NetworkedBuilding building = buildings[i];
                if (building == null || !building.IsActive.Value)
                    continue;

                TowerController tower = building.GetComponent<TowerController>();
                if (tower != null)
                    continue;

                float distance = GetDistanceToTarget(building.transform);
                if (distance > allowedRadius || distance >= nearestDistance)
                    continue;

                nearestDistance = distance;
                nearestEntity = building.transform;
            }
        }

        return registryAvailable;
    }

    private IEnumerator ReturnToPoolAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (EnemyPoolManager.Instance != null)
            EnemyPoolManager.Instance.ReturnToPool(gameObject);
    }

    private void OnDisable()
    {
        HordeManager.UnregisterEnemy(this);
    }

    private void OnDestroy()
    {
        HordeManager.UnregisterEnemy(this);
    }
}
