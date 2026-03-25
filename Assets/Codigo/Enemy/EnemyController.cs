using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using ExoBeasts.Multiplayer.GameServer;

public enum AITargetPriority { Player, Objective }

/// <summary>
/// ── EnemyController ────────────────────────────────────
/// IA de inimigo que roda apenas no servidor (NetworkedEnemy controla enable/disable).
///
///  ▸ Server: decide alvo (jogador mais proximo via PlayerRegistry ou patrulha)
///  ▸ Server: persegue jogador ou segue waypoints ate o objetivo
///  ▸ Suporta status effects: slow, slip, root, knockback, blind, paint stacks
///  ▸ AttackObjectiveAndDie: dano no objetivo e auto-destruicao ao fim da patrulha
/// ─────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    [Header("Inteligência Artificial")]
    public AITargetPriority mainPriority = AITargetPriority.Player;
    public float chaseDistance = 15f;
    public float selfDefenseRadius = 5f;
    public float attackDistance = 2f;
    public float maxChaseTime = 10f;
    public float maxChaseDistance = 20f;

    [Header("Configurações Físicas")]
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
    private Transform playerTransform; // Alvo atual de jogador
    private List<Transform> patrolPoints;
    private int currentPointIndex = 0;
    private Transform lastWaypointReached;

    private float currentMoveSpeed;
    private float speedModifier = 1f;
    private float currentChaseTimer = 0f;
    private Vector3 initialChasePosition;
    private int nivel;
    public EnemyDataSO enemyData { get; private set; }

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

    public void InitializeEnemy(Transform initialTarget, List<Transform> points, EnemyDataSO data, int level)
    {
        playerTransform = initialTarget;
        patrolPoints = points;
        enemyData = data;
        nivel = level;
        currentPointIndex = 0;
        IsDead = false;

        if (healthSystem != null) healthSystem.InitializeHealth(level);
        if (combatSystem != null) combatSystem.InitializeCombat(data, level);

        if (agent != null) agent.speed = originalMoveSpeed;
    }

    void Update()
    {
        if (NetworkManager.Singleton != null && !NetworkManager.Singleton.IsServer) return;
        if (IsDead || isSlipping || isRooted || isKnockedBack) return;

        DecideTarget();

        if (target != null)
        {
            ChaseTarget();
        }
        else
        {
            Patrol();
        }

        if (paintStacks > 0 && Time.time > paintStackResetTime) paintStacks = 0;
    }

    private void DecideTarget()
    {
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

            if (currentChaseTimer >= maxChaseTime || distanceTraveled >= maxChaseDistance || distanceToPlayer > chaseDistance * 1.5f)
            {
                target = null;
                currentChaseTimer = 0f;
            }
        }
        else
        {
            bool shouldChase = false;
            if (mainPriority == AITargetPriority.Player && distanceToPlayer <= chaseDistance) shouldChase = true;
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
        if (PlayerRegistry.Instance == null) return playerTransform;

        var players = PlayerRegistry.Instance.GetAllPlayers();
        if (players.Count == 0) return null;

        float minDistance = float.MaxValue;
        Transform nearest = null;

        foreach (var p in players.Values)
        {
            if (p == null) continue;
            float dist = Vector3.Distance(transform.position, p.transform.position);
            if (dist < minDistance)
            {
                minDistance = dist;
                nearest = p.transform;
            }
        }
        return nearest;
    }

    private void Patrol()
    {
        if (patrolPoints == null || patrolPoints.Count == 0 || currentPointIndex >= patrolPoints.Count)
        {
            if (anim != null) anim.SetBool("isWalking", false);
            AttackObjectiveAndDie();
            return;
        }
        if (anim != null) anim.SetBool("isWalking", true);
        MoveTowardsPosition(patrolPoints[currentPointIndex].position);
    }

    private void ChaseTarget()
    {
        if (target == null) return;

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget <= attackDistance)
        {
            if (agent != null && agent.enabled) agent.isStopped = true;
            
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
            if (agent != null && agent.enabled) agent.isStopped = false;
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
        {
            objective.TakeDamage(enemyData.GetDamage(nivel));
        }
        HandleDeath();
    }

    public void HandleDeath()
    {
        if (IsDead) return;
        IsDead = true;
        
        if (anim != null) anim.SetBool("isWalking", false);
        if (agent != null) agent.isStopped = true;

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
        {
            EnemyPoolManager.Instance.ReturnToPool(gameObject);
        }
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
        {
            rb.AddForce(direction.normalized * force, ForceMode.Impulse);
        }
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
    private IEnumerator RootRoutine(float duration)
    {
        isRooted = true;
        if (agent != null) agent.isStopped = true;
        yield return new WaitForSeconds(duration);
        if (agent != null) agent.isStopped = false;
        isRooted = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (patrolPoints != null && currentPointIndex < patrolPoints.Count)
        {
            if (other.transform == patrolPoints[currentPointIndex])
            {
                currentPointIndex++;
            }
        }
    }
    
    public void SetPatrolPoints(List<Transform> points) => patrolPoints = points;
}
