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

    [Header("Controle de Grupo (CC)")]
    private float speedModifier = 1f;
    private bool isRooted = false;
    private bool isSlipping = false;
    private bool isKnockedBack = false; // NOVA VARIÁVEL
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
    private NavMeshAgent agent;
    private Animator anim;

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

        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.constraints = RigidbodyConstraints.FreezeRotation;
            rb.useGravity = true;
        }

        if (TryGetComponent(out agent))
            rb.isKinematic = true;

        if (!string.IsNullOrEmpty(eventoMonstro))
        {
            monstroSoundInstance = RuntimeManager.CreateInstance(eventoMonstro);
            RuntimeManager.AttachInstanceToGameObject(monstroSoundInstance, transform);
        }
    }

    void OnEnable()
    {
        if (monstroSoundInstance.isValid()) monstroSoundInstance.start();
    }

    void OnDisable()
    {
        if (monstroSoundInstance.isValid()) monstroSoundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
    }

    private void OnDestroy()
    {
        if (monstroSoundInstance.isValid()) monstroSoundInstance.release();
    }

    public void InitializeEnemy(Transform player, List<Transform> path, EnemyDataSO data, int level)
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

        healthSystem.enemyData = this.enemyData;
        healthSystem.InitializeHealth(nivel);
        currentPointIndex = 0;
        target = null;

        if (patrolPoints != null && patrolPoints.Count > 0)
            lastWaypointReached = patrolPoints[0];
        else
            lastWaypointReached = null;

        if (anim == null) anim = GetComponent<Animator>();
        if (anim != null) anim.SetBool("isWalking", false);

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (agent != null)
        {
            agent.ResetPath();
            agent.speed = originalMoveSpeed;
            agent.isStopped = false;
        }

        if (combatSystem != null)
        {
            combatSystem.InitializeCombat(enemyData, nivel);
        }
    }

    void Update()
    {
        if (IsDead) return;

        if (transform.position.y < respawnYThreshold)
        {
            RespawnAtLastWaypoint();
        }

        if (paintStacks > 0 && Time.time > paintStackResetTime)
        {
            paintStacks = 0;
        }
    }

    void FixedUpdate()
    {
        if (IsDead)
        {
            if (anim != null) anim.SetBool("isWalking", false);
            return;
        }

        if (isSlipping || isRooted || isKnockedBack) // SE ESTIVER SENDO EMPURRADO, NÃO MOVE
        {
            if (anim != null) anim.SetBool("isWalking", false);
            return;
        }

        if (target != null && target.CompareTag(TAG_POCA))
        {
            target = null;
        }

        DecideTarget();

        if (target != null)
        {
            ChaseTarget();
        }
        else
        {
            Patrol();
        }
    }

    public void SetBlinded(bool state)
    {
        isBlinded = state;
    }

    // --- CORREÇÃO DO KNOCKBACK ---
    public void ApplyKnockback(Vector3 direction, float force)
    {
        if (rb != null && !isRooted)
        {
            StartCoroutine(KnockbackRoutine(direction, force));
        }
    }

    private IEnumerator KnockbackRoutine(Vector3 direction, float force)
    {
        isKnockedBack = true;

        if (agent != null) agent.isStopped = true;
        rb.isKinematic = false;
        rb.linearVelocity = Vector3.zero;
        rb.AddForce(direction.normalized * force, ForceMode.Impulse);

        yield return new WaitForSeconds(0.4f);

        rb.linearVelocity = Vector3.zero;
        rb.isKinematic = agent != null;
        if (agent != null) agent.isStopped = false;
        isKnockedBack = false;
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
        if (!isSlipping && !isRooted)
        {
            StartCoroutine(SlipRoutine());
        }
    }

    private IEnumerator SlipRoutine()
    {
        isSlipping = true;
        if (anim != null) anim.SetTrigger("Slip");
        if (agent != null) agent.isStopped = true;
        if (rb != null) rb.linearVelocity = Vector3.zero;

        yield return new WaitForSeconds(1.5f);

        if (agent != null) agent.isStopped = false;
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
        if (agent != null) agent.isStopped = true;
        if (rb != null) rb.linearVelocity = Vector3.zero;

        yield return new WaitForSeconds(duration);

        if (agent != null) agent.isStopped = false;
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
        if (playerTransform == null)
        {
            target = null;
            return;
        }

        if (playerTransform.CompareTag(TAG_POCA))
        {
            target = null;
            return;
        }

        float distanceToPlayer = Vector3.Distance(transform.position, playerTransform.position);

        bool foundTarget = false;

        if (mainPriority == AITargetPriority.Player && distanceToPlayer <= chaseDistance)
        {
            target = playerTransform;
            foundTarget = true;
        }
        else if (mainPriority == AITargetPriority.Objective && distanceToPlayer <= selfDefenseRadius)
        {
            target = playerTransform;
            foundTarget = true;
        }

        if (!foundTarget)
        {
            target = null;
        }
    }

    public void LoseTarget()
    {
        target = null;
    }

    public void SetTargetNull()
    {
        target = null;
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

        Transform currentDestination = patrolPoints[currentPointIndex];
        MoveTowardsPosition(currentDestination.position);
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
            if (patrolPoints != null && patrolPoints.Count > 0)
                lastWaypointReached = patrolPoints[0];
            else
            {
                EnemyPoolManager.Instance.ReturnToPool(gameObject);
                return;
            }
        }

        if (rb != null)
        {
            rb.linearVelocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;
        }

        if (agent != null && agent.isOnNavMesh)
            agent.Warp(lastWaypointReached.position);
        else
            transform.position = lastWaypointReached.position;

        target = null;
        if (anim != null) anim.SetBool("isWalking", true);
    }

    private void AttackObjectiveAndDie()
    {
        ObjectiveHealthSystem objective = FindFirstObjectByType<ObjectiveHealthSystem>();
        if (objective != null)
        {
            float damageToObjective = enemyData.GetDamage(nivel);
            objective.TakeDamage(damageToObjective);
        }
        EnemyPoolManager.Instance.ReturnToPool(gameObject);
    }

    private void ChaseTarget()
    {
        if (target == null) return;

        if (target.CompareTag(TAG_POCA))
        {
            target = null;
            return;
        }

        float distanceToTarget = Vector3.Distance(transform.position, target.position);

        if (distanceToTarget <= attackDistance)
        {
            if (agent != null && agent.enabled) agent.isStopped = true;
            if (rb != null && !rb.isKinematic) rb.linearVelocity = new Vector3(0f, rb.linearVelocity.y, 0f);

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
                if (agent != null && agent.enabled)
                    transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.fixedDeltaTime);
                else
                    rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation, 10f * Time.fixedDeltaTime));
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
        if (isRooted || isSlipping || isKnockedBack) return;

        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.speed = currentMoveSpeed * speedModifier;
            agent.SetDestination(targetPosition);
            return;
        }

        if (rb == null) return;

        Vector3 direction = (targetPosition - transform.position).normalized;
        direction.y = 0;

        float finalSpeed = currentMoveSpeed * speedModifier;
        rb.linearVelocity = new Vector3(direction.x * finalSpeed, rb.linearVelocity.y, direction.z * finalSpeed);

        if (direction != Vector3.zero)
        {
            Quaternion targetRotation = Quaternion.LookRotation(direction);
            rb.MoveRotation(Quaternion.Slerp(transform.rotation, targetRotation, 5f * Time.fixedDeltaTime));
        }
    }

    public void HandleDeath()
    {
        if (anim != null) anim.SetBool("isWalking", false);
        if (agent != null) agent.ResetPath();
        EnemyPoolManager.Instance.ReturnToPool(gameObject);
    }

    public void TakeDamage(float damageAmount, Transform attacker = null)
    {
        healthSystem.TakeDamage(damageAmount);

        if (attacker != null && target == null)
        {
            if (attacker.CompareTag(TAG_POCA))
            {
                // Ignora
            }
            else
            {
                target = attacker;
            }
        }
    }

    public void SetPatrolPoints(List<Transform> points)
    {
        patrolPoints = points;
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, chaseDistance);
        Gizmos.color = Color.red;
        if (target != null) Gizmos.DrawLine(transform.position, target.position);
    }
}