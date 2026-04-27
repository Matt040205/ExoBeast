using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using ExoBeasts.Multiplayer.GameServer;

public enum AITargetPriority { Player, Objective }

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    [Header("InteligÃªncia Artificial")]
    public AITargetPriority mainPriority = AITargetPriority.Player;
    public float findDistance = 15f;
    public float attackDistance = 2f;
    public float loseSightDistance = 25f;
    public float selfDefenseRadius = 5f;
    public float maxChaseTime = 10f;
    public float maxChaseDistance = 20f;

    [Header("ConfiguraÃ§Ãµes FÃ­sicas")]
    public float originalMoveSpeed = 3.5f;

    [Header("Status")]
    public bool IsDead = false;
    public bool IsBlinded = false;

    private NavMeshAgent agent;
    private Rigidbody rb;
    private Animator anim;
    private EnemyHealthSystem healthSystem;
    private EnemyCombatSystem combatSystem;

    private Transform target;
    public Transform Target => target;

    private Transform playerTransform;
    private List<Transform> patrolPoints;
    private int currentPointIndex = 0;
    private Transform lastWaypointReached;

    private float currentMoveSpeed;
    private float speedModifier = 1f;
    private float currentChaseTimer = 0f;
    private Vector3 initialChasePosition;
    private int nivel;
    public EnemyDataSO enemyData { get; private set; }

    private int pathIndex;
    private bool hasTriggeredHalfway = false;

    private bool isRooted = false;
    private bool isSlipping = false;
    private bool isKnockedBack = false;
    private bool isSlowed = false;
    private int paintStacks = 0;
    private float paintStackResetTime = 0f;

    private const string TAG_POCA = "Poca";

    void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        rb = GetComponent<Rigidbody>();
        anim = GetComponentInChildren<Animator>();
        healthSystem = GetComponent<EnemyHealthSystem>();
        combatSystem = GetComponent<EnemyCombatSystem>();
        currentMoveSpeed = originalMoveSpeed;
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
        speedModifier = 1f;

        EnemyEvents.OnEnemySpawned?.Invoke(pathIndex + 1);
        isRooted = false;
        isSlipping = false;
        isKnockedBack = false;
        isSlowed = false;
        IsBlinded = false;

        if (healthSystem != null) healthSystem.InitializeHealth(level);
        if (combatSystem != null) combatSystem.InitializeCombat(data, level);

        if (agent != null)
        {
            agent.enabled = false;
            agent.enabled = true;
            agent.Warp(transform.position);
            agent.isStopped = false;
            agent.speed = originalMoveSpeed;
        }

        currentMoveSpeed = originalMoveSpeed;
        StartCoroutine(RefreshTargetAfterDelay(0.5f));
    }

    private IEnumerator RefreshTargetAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        playerTransform = FindNearestPlayer();
    }

    void Update()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer) return;
        if (IsDead || isSlipping || isRooted || isKnockedBack) return;

        DecideTarget();

        if (target != null)
            ChaseTarget();
        else
            Patrol();

        if (paintStacks > 0 && Time.time > paintStackResetTime)
            paintStacks = 0;
    }

    private Coroutine tauntCoroutine;
    public void ApplyTaunt(Transform tauntTarget, float duration)
    {
        if (IsDead) return;
        if (tauntCoroutine != null) StopCoroutine(tauntCoroutine);
        tauntCoroutine = StartCoroutine(TauntRoutine(tauntTarget, duration));
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
        if (tauntCoroutine != null) return;

        Transform nearestPlayer = FindNearestPlayer();
        if (nearestPlayer != null) playerTransform = nearestPlayer;

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

            if (currentChaseTimer >= maxChaseTime || distanceTraveled >= maxChaseDistance || distanceToPlayer > loseSightDistance)
            {
                target = null;
                currentChaseTimer = 0f;
            }
        }
        else
        {
            bool shouldChase = false;
            if (mainPriority == AITargetPriority.Player && distanceToPlayer <= findDistance) shouldChase = true;
            else if (mainPriority == AITargetPriority.Objective && distanceToPlayer <= selfDefenseRadius) shouldChase = true;

            if (shouldChase)
            {
                target = playerTransform;
                initialChasePosition = transform.position;
                currentChaseTimer = 0f;
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
            var players = PlayerRegistry.Instance.GetAllPlayers();
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
            if (anim != null) anim.SetBool("isWalking", false);
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

        Vector3 flatPos = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 flatWaypoint = new Vector3(waypoint.position.x, 0, waypoint.position.z);
        float distToWaypoint = Vector3.Distance(flatPos, flatWaypoint);

        if (distToWaypoint <= 3.0f)
        {
            currentPointIndex++;
            return;
        }

        if (anim != null) anim.SetBool("isWalking", true);
        MoveTowardsPosition(waypoint.position);
    }

    private void ChaseTarget()
    {
        if (target == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget <= attackDistance)
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;

            if (anim != null)
            {
                anim.SetBool("isWalking", false);
                anim.SetTrigger("doAttack");
            }

            Vector3 direction = (target.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
            {
                Quaternion targetRotation = Quaternion.LookRotation(direction);
                transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.deltaTime);
            }
        }
        else
        {
            if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
            if (anim != null) anim.SetBool("isWalking", true);
            MoveTowardsPosition(target.position);
        }
    }

    private void MoveTowardsPosition(Vector3 targetPosition)
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = currentMoveSpeed * speedModifier;
            agent.SetDestination(targetPosition);
        }
    }

    private void AttackObjectiveAndDie()
    {
        var objective = ObjectiveHealthSystem.Instance;
        if (objective != null && enemyData != null)
            objective.TakeDamage(enemyData.GetDamage(nivel));

        EnemyEvents.OnEnemyReachedBase?.Invoke();
        HandleDeath();
    }

    public void HandleDeath()
    {
        if (IsDead) return;
        IsDead = true;

        if (anim != null)
        {
            anim.SetBool("isWalking", false);
            foreach (var param in anim.parameters)
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

    private IEnumerator ReturnToPoolAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (EnemyPoolManager.Instance != null)
            EnemyPoolManager.Instance.ReturnToPool(gameObject);
    }

    public void ApplySlow(float percentage, float duration) { StartCoroutine(SlowRoutine(percentage, duration)); }
    private IEnumerator SlowRoutine(float percentage, float duration)
    {
        speedModifier = 1f - percentage;
        yield return new WaitForSeconds(duration);
        speedModifier = 1f;
    }

    public void ApplySlip() { if (!isSlipping) StartCoroutine(SlipRoutine()); }
    private IEnumerator SlipRoutine()
    {
        isSlipping = true;
        if (anim != null) anim.SetTrigger("Slip");
        if (agent != null) agent.isStopped = true;
        yield return new WaitForSeconds(1.5f);
        if (agent != null) agent.isStopped = false;
        isSlipping = false;
    }

    public void AddPaintStack()
    {
        paintStacks++;
        paintStackResetTime = Time.time + 5f;
        if (paintStacks >= 5) { StartCoroutine(RootRoutine(2f)); paintStacks = 0; }
    }

    public void ApplyKnockback(Vector3 direction, float force)
    {
        if (isKnockedBack) return;
        StartCoroutine(KnockbackRoutine(direction, force));
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, float force)
    {
        isKnockedBack = true;
        if (agent != null) agent.isStopped = true;
        if (rb != null && !rb.isKinematic)
            rb.AddForce(direction.normalized * force, ForceMode.Impulse);

        yield return new WaitForSeconds(0.5f);
        if (agent != null) agent.isStopped = false;
        isKnockedBack = false;
    }

    public void AplicarDesaceleracao(float percentualReducao)
    {
        speedModifier = 1f - Mathf.Clamp01(percentualReducao);
        isSlowed = true;
    }

    public void RemoverDesaceleracao()
    {
        speedModifier = 1f;
        isSlowed = false;
    }

    public void ApplyStun(float duration) { if (!IsDead) StartCoroutine(RootRoutine(duration)); }

    private IEnumerator RootRoutine(float duration)
    {
        isRooted = true;
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = true;
        yield return new WaitForSeconds(duration);
        if (agent != null && agent.enabled && agent.isOnNavMesh) agent.isStopped = false;
        isRooted = false;
    }

    public void SetPatrolPoints(List<Transform> points) => patrolPoints = points;

    public void LoseTarget()
    {
        target = null;
        playerTransform = null;
        currentChaseTimer = 0f;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();
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
}
