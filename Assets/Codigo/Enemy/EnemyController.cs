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
    private float currentChaseTimer;
    private Vector3 initialChasePosition;
    private int nivel;
    private int pathIndex;
    private bool hasTriggeredHalfway;
    private int paintStacks;
    private float paintStackResetTime;

    private Coroutine tauntCoroutine;
    private Coroutine aiTickCoroutine;
    private Vector3 lastDestinationSet = Vector3.positiveInfinity;
    
    private Vector3 lastTickPosition;
    private int stuckTickCount;

    private const string TAG_POCA = "Poca";

    public EnemyDataSO enemyData { get; private set; }

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        anim = GetComponentInChildren<Animator>();
        healthSystem = GetComponent<EnemyHealthSystem>();
        combatSystem = GetComponent<EnemyCombatSystem>();
        statusController = GetComponent<EnemyStatusController>();
        networkedEnemy = GetComponent<ExoBeasts.Multiplayer.Sync.NetworkedEnemy>();

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
        currentChaseTimer = 0f;
        paintStacks = 0;
        paintStackResetTime = 0f;
        lastDestinationSet = Vector3.positiveInfinity;
        lastTickPosition = transform.position;
        stuckTickCount = 0;

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

        if (aiTickCoroutine != null)
            StopCoroutine(aiTickCoroutine);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
        {
            aiTickCoroutine = StartCoroutine(AI_TickRoutine());
        }
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
            }

            DecideTargetTick();

            if (target != null)
            {
                if (agent.isStopped)
                {
                    if (!IsTargetInDisengageRange())
                    {
                        ResumeMovement();
                        MoveTowardsPositionTick(target.position);
                    }
                    else
                    {
                        FaceTarget();
                    }
                }
                else
                {
                    if (IsTargetInAttackRange())
                    {
                        HoldAttackPosition();
                    }
                    else
                    {
                        ResumeMovement();
                        MoveTowardsPositionTick(target.position);
                    }
                }
            }
            else
            {
                ResumeMovement();
                PatrolTick();
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

    // Buffer pre-alocado para Physics.OverlapSphereNonAlloc — evita GC todo tick.
    // 64 colliders é generoso para um raio de visão típico de inimigo (findDistance ~15m).
    private static readonly Collider[] _targetingBuffer = new Collider[64];

    private void DecideTargetTick()
    {
        float allowedRadius = (mainPriority == AITargetPriority.Player) ? findDistance : selfDefenseRadius;

        Transform nearestEntity = null;
        float nearestDistance = float.MaxValue;

        // 1. Busca Jogadores via PlayerRegistry (fonte autoritativa server-side).
        // Em singleplayer (sem NGO), PlayerRegistry pode não existir — caímos no fallback
        // por tag UMA VEZ apenas (em vez de todo tick) via early return abaixo.
        if (PlayerRegistry.Instance != null && PlayerRegistry.Instance.GetPlayerCount() > 0)
        {
            foreach (GameObject playerObject in PlayerRegistry.Instance.GetAllPlayers().Values)
            {
                if (playerObject == null || playerObject.CompareTag(TAG_POCA))
                    continue;

                float distance = Vector3.Distance(transform.position, playerObject.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestEntity = playerObject.transform;
                }
            }
        }
        else
        {
            // Fallback singleplayer: registry vazio = não há rede ativa, então cai no FindWithTag.
            // FindGameObjectsWithTag (plural) percorre toda a hierarchy a cada chamada — 90+ scans/s
            // em wave de 30 inimigos. Aqui usamos FindWithTag (singular) que para no primeiro match.
            GameObject fallbackPlayer = GameObject.FindWithTag("Player");
            if (fallbackPlayer != null && !fallbackPlayer.CompareTag(TAG_POCA))
            {
                float distance = Vector3.Distance(transform.position, fallbackPlayer.transform.position);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestEntity = fallbackPlayer.transform;
                }
            }
        }

        // 2. Busca Torres (apenas dentro do allowedRadius para otimizar Physics).
        // OverlapSphereNonAlloc reutiliza o buffer estático — zero alocação por tick.
        int hitCount = Physics.OverlapSphereNonAlloc(transform.position, allowedRadius, _targetingBuffer);
        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _targetingBuffer[i];
            if (col == null) continue;

            TowerController tower = col.GetComponent<TowerController>();
            if (tower != null)
            {
                // Ignora torres destruídas que ainda existem na cena (pooling)
                if (tower.IsDestroyed) continue;

                float distance = GetDistanceToTarget(col.transform);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestEntity = col.transform;
                }
                continue;
            }

            if (col.GetComponent<ExoBeasts.Multiplayer.Sync.NetworkedBuilding>() != null)
            {
                float distance = GetDistanceToTarget(col.transform);
                if (distance < nearestDistance)
                {
                    nearestDistance = distance;
                    nearestEntity = col.transform;
                }
            }
        }

        // 3. Decisão do Alvo
        if (nearestEntity != null && nearestDistance <= allowedRadius)
        {
            if (target != nearestEntity)
            {
                // Novo alvo adquirido
                target = nearestEntity;
                initialChasePosition = transform.position;
                currentChaseTimer = 0f;
                SetAggroVisual(true);
            }
            else
            {
                // Mantém perseguição e aplica regras de tempo/distância máxima
                currentChaseTimer += 0.3f;
                float distanceTraveled = Vector3.Distance(transform.position, initialChasePosition);

                if (currentChaseTimer >= maxChaseTime ||
                    distanceTraveled >= maxChaseDistance ||
                    nearestDistance > loseSightDistance)
                {
                    target = null;
                    currentChaseTimer = 0f;
                    SetAggroVisual(false);
                }
            }
        }
        else
        {
            // Ninguém dentro do allowedRadius
            target = null;
            currentChaseTimer = 0f;
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
        Vector3 flatWaypoint  = new Vector3(waypoint.position.x, 0f, waypoint.position.z);
        float distanceToWaypoint = Vector3.Distance(flatPosition, flatWaypoint);

        // Avança para o próximo waypoint apenas quando chegou perto o suficiente do atual.
        // REMOVIDO: a lógica que comparava distâncias com o próximo ponto causava
        // skip de waypoints — o inimigo pulava pontos no meio do caminho.
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

        // Durante a patrulha o agente não deve parar antes dos waypoints.
        // Durante o combate usa attackDistance para parar na distância correta.
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
        // Ignora a altura (Y) para não inflar a distância quando o pivot do jogador
        // está mais alto (ex: no peito) e o inimigo está no chão.
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

        if (aiTickCoroutine != null)
            StopCoroutine(aiTickCoroutine);

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
        currentChaseTimer = 0f;
        lastDestinationSet = Vector3.positiveInfinity;
        SetAggroVisual(false);

        if (agent != null && agent.enabled && agent.isOnNavMesh)
            agent.ResetPath();
    }

    private void SetAggroVisual(bool isActive)
    {
        // BUG FIX (Bugs 4 e 5 - 7 Maio 2026): antes SetAggroVisual so rodava local-side no servidor
        // (porque AI_TickRoutine so eh server-side). Resultado: cliente nunca via o ponto de
        // exclamacao quando o inimigo o detectava — parecia que o inimigo nao detectava o jogador 2.
        // Agora o servidor aplica local + broadcast via ClientRpc para sincronizar todos os clientes.
        SetAggroVisualLocal(isActive);

        if (networkedEnemy != null && networkedEnemy.IsSpawned)
            networkedEnemy.SetAggroVisualClientRpc(isActive);
    }

    /// <summary>
    /// Aplica APENAS o efeito visual do aggro (set active do indicador). Chamado tanto local-side
    /// (servidor em SetAggroVisual) quanto via ClientRpc broadcast (clientes em SetAggroVisualClientRpc).
    /// Publico porque NetworkedEnemy chama via GetComponent — EnemyController fica disabled em
    /// clientes nao-servidor mas metodos publicos podem ser invocados manualmente.
    /// </summary>
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
        currentChaseTimer = 0f;
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
