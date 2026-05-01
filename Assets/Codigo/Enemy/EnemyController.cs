using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;
using ExoBeasts.Multiplayer.GameServer;

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

    private Transform target;
    private Transform playerTransform;
    private List<Transform> patrolPoints;
    private int currentPointIndex;
    private float currentChaseTimer;
    private Vector3 initialChasePosition;
    private int nivel;
    private int pathIndex;
    private bool hasTriggeredHalfway;
    private int paintStacks;
    private float paintStackResetTime;

    private Coroutine tauntCoroutine;

    private const string TAG_POCA = "Poca";

    public EnemyDataSO enemyData { get; private set; }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        healthSystem = GetComponent<EnemyHealthSystem>();
        combatSystem = GetComponent<EnemyCombatSystem>();
        statusController = GetComponent<EnemyStatusController>();

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
        target = null;
        currentChaseTimer = 0f;
        paintStacks = 0;
        paintStackResetTime = 0f;

        SetAggroVisual(false);

        EnemyEvents.OnEnemySpawned?.Invoke(pathIndex + 1);
        statusController.ResetState();

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

        StartCoroutine(RefreshTargetAfterDelay(0.5f));
    }

    private IEnumerator RefreshTargetAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        playerTransform = FindNearestPlayer();
    }

    private void Update()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer)
            return;

        if (IsDead)
            return;

        DecideTarget();

        if (paintStacks > 0 && Time.time > paintStackResetTime)
            paintStacks = 0;

        if (statusController != null && !statusController.CanMove)
        {
            FaceTarget();
            return;
        }

        if (target != null)
            ChaseTarget();
        else
            Patrol();
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

        return Vector3.Distance(transform.position, currentTarget.position) <= attackDistance;
    }

    public void HoldAttackPosition()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.isStopped = true;

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

        if (anim != null)
        {
            anim.SetBool("isWalking", false);
            foreach (AnimatorControllerParameter param in anim.parameters)
            {
                if (param.name == "isDead" && param.type == AnimatorControllerParameterType.Trigger)
                {
                    anim.SetTrigger("isDead");
                    break;
                }
            }
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
        playerTransform = null;
        currentChaseTimer = 0f;
        SetAggroVisual(false);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();
    }

    private void SetAggroVisual(bool isActive)
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

        playerTransform = FindNearestPlayer();
        target = null;
        currentChaseTimer = 0f;
        DecideTarget();
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

    private void DecideTarget()
    {
        if (tauntCoroutine != null)
            return;

        Transform nearestPlayer = FindNearestPlayer();
        if (nearestPlayer != null)
            playerTransform = nearestPlayer;

        if (playerTransform == null || playerTransform.CompareTag(TAG_POCA))
        {
            target = null;
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);
        if (target == playerTransform)
        {
            currentChaseTimer += Time.deltaTime;
            float distanceTraveled = Vector3.Distance(transform.position, initialChasePosition);

            if (currentChaseTimer >= maxChaseTime ||
                distanceTraveled >= maxChaseDistance ||
                distanceToPlayer > loseSightDistance)
            {
                target = null;
                currentChaseTimer = 0f;
                SetAggroVisual(false);
            }
        }
        else
        {
            bool shouldChase = false;
            if (mainPriority == AITargetPriority.Player && distanceToPlayer <= findDistance)
                shouldChase = true;
            else if (mainPriority == AITargetPriority.Objective && distanceToPlayer <= selfDefenseRadius)
                shouldChase = true;

            if (shouldChase)
            {
                target = playerTransform;
                initialChasePosition = transform.position;
                currentChaseTimer = 0f;
                SetAggroVisual(true);
            }
        }
    }

    private Transform FindNearestPlayer()
    {
        Transform nearestVisiblePlayer = null;
        float nearestVisibleDistance = float.MaxValue;
        Transform nearestPocaPlayer = null;
        float nearestPocaDistance = float.MaxValue;

        if (PlayerRegistry.Instance != null)
        {
            Dictionary<ulong, GameObject> players = PlayerRegistry.Instance.GetAllPlayers();
            if (players.Count > 0)
            {
                foreach (GameObject playerObject in players.Values)
                {
                    if (playerObject == null)
                        continue;

                    float distance = Vector3.Distance(transform.position, playerObject.transform.position);
                    if (playerObject.CompareTag(TAG_POCA))
                    {
                        if (distance < nearestPocaDistance)
                        {
                            nearestPocaDistance = distance;
                            nearestPocaPlayer = playerObject.transform;
                        }

                        continue;
                    }

                    if (distance < nearestVisibleDistance)
                    {
                        nearestVisibleDistance = distance;
                        nearestVisiblePlayer = playerObject.transform;
                    }
                }

                if (nearestVisiblePlayer != null)
                    return nearestVisiblePlayer;

                if (nearestPocaPlayer != null)
                    return nearestPocaPlayer;
            }
        }

        GameObject[] fallbackPlayers = GameObject.FindGameObjectsWithTag("Player");
        foreach (GameObject playerObject in fallbackPlayers)
        {
            if (playerObject == null)
                continue;

            float distance = Vector3.Distance(transform.position, playerObject.transform.position);
            if (playerObject.CompareTag(TAG_POCA))
            {
                if (distance < nearestPocaDistance)
                {
                    nearestPocaDistance = distance;
                    nearestPocaPlayer = playerObject.transform;
                }

                continue;
            }

            if (distance < nearestVisibleDistance)
            {
                nearestVisibleDistance = distance;
                nearestVisiblePlayer = playerObject.transform;
            }
        }

        if (nearestVisiblePlayer != null)
            return nearestVisiblePlayer;

        return nearestPocaPlayer != null ? nearestPocaPlayer : playerTransform;
    }

    private void Patrol()
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

        if (distanceToWaypoint <= 3f)
        {
            currentPointIndex++;
            return;
        }

        if (anim != null)
            anim.SetBool("isWalking", true);

        MoveTowardsPosition(waypoint.position);
    }

    private void ChaseTarget()
    {
        if (target == null)
            return;

        if (IsTargetInAttackRange())
        {
            HoldAttackPosition();
            return;
        }

        ResumeMovement();

        if (anim != null)
            anim.SetBool("isWalking", true);

        MoveTowardsPosition(target.position);
    }

    private void MoveTowardsPosition(Vector3 targetPosition)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        float speedMultiplier = statusController != null ? statusController.SpeedModifier : 1f;
        agent.speed = originalMoveSpeed * speedMultiplier;
        agent.SetDestination(targetPosition);
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

    private void AttackObjectiveAndDie()
    {
        ObjectiveHealthSystem objective = ObjectiveHealthSystem.Instance;
        if (objective != null && enemyData != null)
            objective.TakeDamage(enemyData.GetDamage(nivel));

        EnemyEvents.OnEnemyReachedBase?.Invoke();
        HandleDeath();
    }

    private IEnumerator ReturnToPoolAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (EnemyPoolManager.Instance != null)
            EnemyPoolManager.Instance.ReturnToPool(gameObject);
    }
}
