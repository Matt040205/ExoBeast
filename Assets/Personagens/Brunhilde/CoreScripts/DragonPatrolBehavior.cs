using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Movimenta a Torre Dragao como um ator server-authoritative.
/// Ela patrulha a partir do ponto de spawn e nunca persegue fora desse leash.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class DragonPatrolBehavior : MonoBehaviour
{
    private enum PatrolState
    {
        Idle,
        Chasing,
        Attacking,
        Returning
    }

    [Header("Movimentacao")]
    public float moveSpeed = 0.5f;
    public float patrolVisionRadius = 15f;

    [SerializeField] private float homeArrivalDistance = 0.5f;
    [SerializeField] private float leashTolerance = 0.15f;

    private static readonly Collider[] patrolBuffer = new Collider[96];

    private Vector3 homePosition;
    private bool homePositionInitialized;
    private NavMeshAgent agent;
    private TowerController tower;
    private Transform currentTarget;
    private PatrolState state = PatrolState.Idle;
    private float lastKnownTowerRange = -1f;

    void Awake()
    {
        tower = GetComponent<TowerController>();
        agent = GetComponent<NavMeshAgent>();
    }

    void Start()
    {
        DisableNavMeshObstacles();

        if (!HasPatrolAuthority())
        {
            if (agent != null)
                agent.enabled = false;

            enabled = false;
            return;
        }

        InitializeAgentAtHome();
    }

    void Update()
    {
        if (!HasPatrolAuthority())
            return;

        if (tower == null || agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        if (tower.IsDestroyed || tower.IsMaterializing)
        {
            currentTarget = null;
            SetState(PatrolState.Idle);
            StopAgent();
            return;
        }

        UpdateStoppingDistance();
        RefreshTarget();
        TickState();
    }

    public bool IsTargetInsidePatrolLeash(Transform candidate)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy)
            return false;

        Vector3 leashCenter = homePositionInitialized ? homePosition : transform.position;
        Vector3 closestPoint = GetClosestPoint(candidate, leashCenter);
        return Vector3.Distance(leashCenter, closestPoint) <= patrolVisionRadius + leashTolerance;
    }

    private void InitializeAgentAtHome()
    {
        if (agent == null)
            return;

        homePosition = transform.position;
        homePositionInitialized = true;

        if (!NavMesh.SamplePosition(homePosition, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            Debug.LogWarning($"[DragonPatrol] Construido muito longe do NavMesh. Patrulha desativada em {gameObject.name}.");
            agent.enabled = false;
            return;
        }

        agent.Warp(hit.position);
        homePosition = hit.position;
        agent.speed = moveSpeed;
        agent.autoBraking = true;
        UpdateStoppingDistance(force: true);
        SetState(PatrolState.Idle);
    }

    private void DisableNavMeshObstacles()
    {
        foreach (NavMeshObstacle obstacle in GetComponentsInChildren<NavMeshObstacle>(true))
            obstacle.enabled = false;
    }

    private void RefreshTarget()
    {
        if (IsValidTarget(currentTarget))
            return;

        currentTarget = null;

        Transform towerTarget = tower != null ? tower.TargetEnemy : null;
        if (IsValidTarget(towerTarget))
        {
            currentTarget = towerTarget;
            return;
        }

        currentTarget = FindNearestValidTarget();
    }

    private Transform FindNearestValidTarget()
    {
        Transform bestTarget = null;
        float bestDistance = Mathf.Infinity;

        IReadOnlyList<EnemyController> enemies = HordeManager.GetActiveEnemies();
        if (enemies != null && enemies.Count > 0)
        {
            for (int i = 0; i < enemies.Count; i++)
            {
                EnemyController enemy = enemies[i];
                if (enemy == null || !IsValidTarget(enemy.transform))
                    continue;

                float distance = Vector3.Distance(transform.position, GetClosestPoint(enemy.transform, transform.position));
                if (distance < bestDistance)
                {
                    bestDistance = distance;
                    bestTarget = enemy.transform;
                }
            }

            return bestTarget;
        }

        int hitCount = Physics.OverlapSphereNonAlloc(homePosition, patrolVisionRadius, patrolBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = patrolBuffer[i];
            patrolBuffer[i] = null;

            if (col == null)
                continue;

            Transform candidate = ResolveEnemyTransform(col.transform);
            if (!IsValidTarget(candidate))
                continue;

            float distance = Vector3.Distance(transform.position, GetClosestPoint(candidate, transform.position));
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestTarget = candidate;
            }
        }

        return bestTarget;
    }

    private void TickState()
    {
        float distanceToHome = Vector3.Distance(transform.position, homePosition);

        if (currentTarget == null)
        {
            if (distanceToHome > homeArrivalDistance)
            {
                SetState(PatrolState.Returning);
                MoveTo(homePosition);
            }
            else
            {
                SetState(PatrolState.Idle);
                StopAgent();
            }

            return;
        }

        float targetDistance = Vector3.Distance(transform.position, GetClosestPoint(currentTarget, transform.position));
        if (targetDistance <= tower.CurrentRange)
        {
            SetState(PatrolState.Attacking);
            StopAgent();
            FaceTarget(currentTarget);
            return;
        }

        SetState(PatrolState.Chasing);
        MoveTo(currentTarget.position);
    }

    private void MoveTo(Vector3 destination)
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.isStopped = false;
        agent.updateRotation = state != PatrolState.Attacking;
        agent.SetDestination(destination);
    }

    private void StopAgent()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
            return;

        agent.isStopped = true;
        agent.ResetPath();
    }

    private void FaceTarget(Transform target)
    {
        if (target == null)
            return;

        Vector3 direction = target.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            Quaternion.LookRotation(direction),
            Time.deltaTime * 540f);
    }

    private bool IsValidTarget(Transform candidate)
    {
        if (candidate == null || !candidate.gameObject.activeInHierarchy)
            return false;

        if (!IsTargetInsidePatrolLeash(candidate))
            return false;

        EnemyHealthSystem health = ResolveEnemyHealth(candidate);
        if (health == null || health.isDead)
            return false;

        EnemyController enemy = ResolveEnemyController(candidate);
        if (enemy == null || enemy.IsDead || enemy.enemyData == null)
            return false;

        EnemyType enemyType = enemy.enemyData.enemyType;
        return enemyType == EnemyType.Terrestre || (tower != null && tower.TargetsFlyingEnemies && enemyType == EnemyType.Voador);
    }

    private Transform ResolveEnemyTransform(Transform candidate)
    {
        if (candidate == null)
            return null;

        EnemyController controller = ResolveEnemyController(candidate);
        if (controller != null)
            return controller.transform;

        EnemyHealthSystem health = ResolveEnemyHealth(candidate);
        return health != null ? health.transform : candidate;
    }

    private EnemyController ResolveEnemyController(Transform candidate)
    {
        if (candidate == null)
            return null;

        EnemyController controller = candidate.GetComponent<EnemyController>();
        if (controller == null)
            controller = candidate.GetComponentInParent<EnemyController>();
        if (controller == null)
            controller = candidate.GetComponentInChildren<EnemyController>();

        return controller;
    }

    private EnemyHealthSystem ResolveEnemyHealth(Transform candidate)
    {
        if (candidate == null)
            return null;

        EnemyHealthSystem health = candidate.GetComponent<EnemyHealthSystem>();
        if (health == null)
            health = candidate.GetComponentInParent<EnemyHealthSystem>();
        if (health == null)
            health = candidate.GetComponentInChildren<EnemyHealthSystem>();

        return health;
    }

    private Vector3 GetClosestPoint(Transform candidate, Vector3 origin)
    {
        if (candidate == null)
            return origin;

        Collider col = candidate.GetComponentInChildren<Collider>();
        return col != null ? col.ClosestPoint(origin) : candidate.position;
    }

    private void UpdateStoppingDistance(bool force = false)
    {
        if (agent == null || tower == null)
            return;

        if (!force && Mathf.Approximately(lastKnownTowerRange, tower.CurrentRange))
            return;

        lastKnownTowerRange = tower.CurrentRange;
        agent.stoppingDistance = Mathf.Max(0.35f, tower.CurrentRange * 0.8f);
    }

    private void SetState(PatrolState nextState)
    {
        if (state == nextState)
            return;

        state = nextState;
    }

    private bool HasPatrolAuthority()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return true;

        return NetworkManager.Singleton.IsServer;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Vector3 center = Application.isPlaying && homePositionInitialized ? homePosition : transform.position;
        Gizmos.DrawWireSphere(center, patrolVisionRadius);
    }
}
