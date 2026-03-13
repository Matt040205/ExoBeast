using UnityEngine;
using UnityEngine.AI;
using System.Collections.Generic;
using System.Collections;
using FMODUnity;
using FMOD.Studio;

public enum AITargetPriority
{
    Player,
    Objective
}

[RequireComponent(typeof(NavMeshAgent))]
public class EnemyController : MonoBehaviour
{
    [Header("Dados do Inimigo")]
    public EnemyDataSO enemyData;

    [Header("Status Atual")]
    public int nivel = 1;

    [Header("Pontos de Patrulha")]
    public List<Transform> patrolPoints;

    [Header("Inteligência Artificial")]
    public AITargetPriority mainPriority = AITargetPriority.Objective;
    public float selfDefenseRadius = 5f;

    [Header("Configurações")]
    public float chaseDistance = 50f;
    public float attackDistance = 2f;
    public float respawnYThreshold = -10f;

    [Header("Perseguição (Player)")]
    public float maxChaseTime = 10f;
    public float maxChaseDistance = 60f;
    private float currentChaseTimer = 0f;
    private Vector3 initialChasePosition;

    [Header("Anti-Stuck")]
    public float stuckCheckInterval = 1f;
    public float minDistanceMoved = 0.5f;
    private Vector3 lastStuckCheckPosition;
    private float stuckTimer = 0f;

    [Header("Controle de Grupo (CC)")]
    private float speedModifier = 1f;
    private bool isRooted = false;
    private bool isSlipping = false;
    private bool isKnockedBack = false;
    private int paintStacks = 0;
    private float paintStackResetTime;

    private bool isBlinded = false;
    public bool IsBlinded => isBlinded;

    [Header("FMOD")]
    [EventRef]
    public string eventoMonstro = "event:/SFX/Monstro";
    private EventInstance monstroSoundInstance;

    private EnemyHealthSystem healthSystem;
    private EnemyCombatSystem combatSystem;
    private Rigidbody rb;
    private Animator anim;
    private NavMeshAgent agent;

    private float currentMoveSpeed;
    private float originalMoveSpeed;
    private bool isSlowed = false;

    private int currentPointIndex = 0;
    private Transform target;
    private Transform playerTransform;
    private Transform lastWaypointReached;

    private const string TAG_POCA = "Poca";

    public bool IsDead { get { return healthSystem.isDead; } }
    public Transform Target { get { return target; } }

    void Awake()
    {
        healthSystem = GetComponent<EnemyHealthSystem>();
        combatSystem = GetComponent<EnemyCombatSystem>();
        rb = GetComponent<Rigidbody>();
        anim = GetComponent<Animator>();
        agent = GetComponent<NavMeshAgent>();

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.useGravity = true;
        }

        if (agent != null)
        {
            agent.updateRotation = true;
            agent.updatePosition = true;
        }

        if (!string.IsNullOrEmpty(eventoMonstro))
        {
            monstroSoundInstance = RuntimeManager.CreateInstance(eventoMonstro);
            RuntimeManager.AttachInstanceToGameObject(monstroSoundInstance, transform);
        }
    }

    void OnEnable()
    {
        if (monstroSoundInstance.isValid()) monstroSoundInstance.start();
        lastStuckCheckPosition = transform.position;
    }

    void OnDisable()
    {
        if (monstroSoundInstance.isValid()) monstroSoundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
    }

    private void OnDestroy()
    {
        if (monstroSoundInstance.isValid()) monstroSoundInstance.release();
    }

    public void InitializeEnemy(Transform player, List<Transform> path, EnemyDataSO data, int level, Transform spawnPoint = null)
    {
        this.playerTransform = player;
        this.patrolPoints = path;
        this.enemyData = data;
        this.nivel = level;

        if (enemyData == null)
        {
            gameObject.SetActive(false);
            return;
        }

        originalMoveSpeed = enemyData.GetMoveSpeed(nivel);
        currentMoveSpeed = originalMoveSpeed;
        isSlowed = false;

        speedModifier = 1f;
        isRooted = false;
        isSlipping = false;
        isBlinded = false;
        isKnockedBack = false;
        paintStacks = 0;
        currentChaseTimer = 0f;

        if (agent != null)
        {
            agent.enabled = true;
            if (spawnPoint != null)
                agent.Warp(spawnPoint.position);
            else if (patrolPoints != null && patrolPoints.Count > 0)
                agent.Warp(patrolPoints[0].position);

            agent.speed = originalMoveSpeed;
            agent.stoppingDistance = attackDistance;
        }

        healthSystem.enemyData = this.enemyData;
        healthSystem.InitializeHealth(nivel);
        currentPointIndex = 0;
        target = null;
        lastWaypointReached = spawnPoint != null ? spawnPoint : (patrolPoints != null && patrolPoints.Count > 0 ? patrolPoints[0] : null);

        if (anim == null) anim = GetComponent<Animator>();
        if (anim != null) anim.SetBool("isWalking", false);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (combatSystem != null) combatSystem.InitializeCombat(enemyData, nivel);
    }

    void Update()
    {
        if (IsDead)
        {
            if (agent.enabled) agent.isStopped = true;
            return;
        }

        if (transform.position.y < respawnYThreshold) RespawnAtLastWaypoint();

        if (paintStacks > 0 && Time.time > paintStackResetTime) paintStacks = 0;

        bool canMove = !(isSlipping || isRooted || isKnockedBack);

        if (agent.enabled)
        {
            agent.isStopped = !canMove;
            agent.speed = currentMoveSpeed * speedModifier;
            CheckIfStuck();
        }

        if (!canMove)
        {
            if (anim != null) anim.SetBool("isWalking", false);
            return;
        }

        DecideTarget();

        if (target != null) ChaseTarget();
        else Patrol();
    }

    private void CheckIfStuck()
    {
        if (!agent.hasPath || agent.velocity.sqrMagnitude > 0.1f)
        {
            stuckTimer = 0;
            return;
        }

        stuckTimer += Time.deltaTime;

        if (stuckTimer >= stuckCheckInterval)
        {
            float dist = Vector3.Distance(transform.position, lastStuckCheckPosition);
            if (dist < minDistanceMoved)
            {
                if (target != null)
                {
                    agent.ResetPath();
                    target = null;
                }
                else
                {
                    currentPointIndex = (currentPointIndex + 1) % patrolPoints.Count;
                }
            }
            lastStuckCheckPosition = transform.position;
            stuckTimer = 0;
        }
    }

    public void SetBlinded(bool state) => isBlinded = state;

    public void ApplyKnockback(Vector3 direction, float force)
    {
        if (rb != null && !isRooted) StartCoroutine(KnockbackRoutine(direction, force));
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, float force)
    {
        isKnockedBack = true;
        if (agent.enabled) agent.enabled = false;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);
        yield return new WaitForSeconds(0.4f);
        yield return new WaitUntil(() => Mathf.Abs(rb.linearVelocity.y) < 0.1f);
        rb.isKinematic = true;
        isKnockedBack = false;
        if (NavMesh.SamplePosition(transform.position, out NavMeshHit hit, 2.0f, NavMesh.AllAreas))
        {
            agent.enabled = true;
            agent.Warp(hit.position);
        }
    }

    public void ApplySlow(float percentage, float duration)
    {
        StopCoroutine("SlowRoutine");
        StartCoroutine(SlowRoutine(percentage, duration));
    }

    private IEnumerator SlowRoutine(float percentage, float duration)
    {
        speedModifier = Mathf.Clamp01(1f - percentage);
        if (anim != null) anim.speed = speedModifier;
        yield return new WaitForSeconds(duration);
        speedModifier = 1f;
        if (anim != null) anim.speed = 1f;
    }

    public void ApplySlip()
    {
        if (!isSlipping && !isRooted) StartCoroutine(SlipRoutine());
    }

    private IEnumerator SlipRoutine()
    {
        isSlipping = true;
        if (anim != null) anim.SetTrigger("Slip");
        if (agent.enabled) agent.isStopped = true;
        yield return new WaitForSeconds(1.5f);
        isSlipping = false;
    }

    public void AddPaintStack()
    {
        paintStacks++;
        paintStackResetTime = Time.time + 5f;
        if (paintStacks >= 5)
        {
            StartCoroutine(RootRoutine(2f));
            paintStacks = 0;
        }
    }

    private IEnumerator RootRoutine(float duration)
    {
        isRooted = true;
        if (agent.enabled) agent.isStopped = true;
        yield return new WaitForSeconds(duration);
        isRooted = false;
    }

    public void AplicarDesaceleracao(float percentual)
    {
        if (!isSlowed)
        {
            currentMoveSpeed = originalMoveSpeed * (1f - percentual);
            isSlowed = true;
        }
    }

    public void RemoverDesaceleracao()
    {
        if (isSlowed)
        {
            currentMoveSpeed = originalMoveSpeed;
            isSlowed = false;
        }
    }

    private void DecideTarget()
    {
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
                return;
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

    public void LoseTarget() => target = null;
    public void SetTargetNull() => target = null;

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
            if (agent.enabled) agent.isStopped = true;
            if (anim != null)
            {
                anim.SetBool("isWalking", false);
                anim.SetTrigger("doAttack");
            }
            Vector3 direction = (target.position - transform.position).normalized;
            direction.y = 0;
            if (direction != Vector3.zero)
                transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(direction), 10f * Time.deltaTime);
        }
        else
        {
            if (anim != null) anim.SetBool("isWalking", true);
            MoveTowardsPosition(target.position);
        }
    }

    private void MoveTowardsPosition(Vector3 targetPosition)
    {
        if (agent.enabled)
        {
            agent.isStopped = false;
            agent.SetDestination(targetPosition);
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (patrolPoints != null && currentPointIndex < patrolPoints.Count)
        {
            if (other.transform == patrolPoints[currentPointIndex])
            {
                lastWaypointReached = patrolPoints[currentPointIndex];
                currentPointIndex++;
            }
        }
    }

    private void RespawnAtLastWaypoint()
    {
        if (lastWaypointReached == null)
        {
            if (patrolPoints != null && patrolPoints.Count > 0) lastWaypointReached = patrolPoints[0];
            else
            {
                EnemyPoolManager.Instance.ReturnToPool(gameObject);
                return;
            }
        }
        if (agent.enabled) agent.Warp(lastWaypointReached.position);
        else transform.position = lastWaypointReached.position;
        target = null;
    }

    private void AttackObjectiveAndDie()
    {
        ObjectiveHealthSystem objective = FindFirstObjectByType<ObjectiveHealthSystem>();
        if (objective != null) objective.TakeDamage(enemyData.GetDamage(nivel));
        EnemyPoolManager.Instance.ReturnToPool(gameObject);
    }

    public void HandleDeath()
    {
        if (anim != null) anim.SetBool("isWalking", false);
        if (agent.enabled) agent.isStopped = true;
        EnemyPoolManager.Instance.ReturnToPool(gameObject);
    }

    public void TakeDamage(float damageAmount, Transform attacker = null)
    {
        healthSystem.TakeDamage(damageAmount);
        if (attacker != null && target == null && !attacker.CompareTag(TAG_POCA)) target = attacker;
    }

    public void SetPatrolPoints(List<Transform> points) => patrolPoints = points;

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseDistance);
        Gizmos.color = Color.red;
        if (target != null) Gizmos.DrawLine(transform.position, target.position);
    }
}