using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using Unity.Netcode;
using ExoBeasts.Multiplayer.Sync;
using FMODUnity;

public class TowerController : MonoBehaviour
{
    [Header("ReferÃªncias Principais")]
    public CharacterBase towerData;
    public Transform partToRotate;
    public Transform firePoint;

    [Header("MaterializaÃ§Ã£o (Spawn)")]
    [SerializeField] private float tempoDeSpawn = 2f;
    [SerializeField] private Material materialHolograma;
    [SerializeField] private Material materialToon;
    [SerializeField] private Material materialOutline;
    private bool isMaterializing = false;
    public bool IsMaterializing => isMaterializing;

    [Header("Visual e AnimaÃ§Ã£o")]
    public Animator animator;
    public string shootTrigger = "Attack";
    [SerializeField] private bool playAttackAnimation = true;
    public string towerModeBool = "IsTower";
    public Vector3 rotationOffset;

    [Header("FMOD - Sons")]
    [EventRef] public string somTiro = "event:/SFX/Towers/Shot_Magic";

    [Header("ConfiguraÃ§Ãµes de IA")]
    [SerializeField] private string enemyTag = "Enemy";
    public bool TargetsFlyingEnemies { get; set; } = false;

    // Eventos
    public event Action<EnemyHealthSystem> OnTargetDamaged;
    public event Func<EnemyHealthSystem, float, float> OnCalculateDamage;
    public event Action<EnemyHealthSystem> OnCriticalHit;
    public event Action<EnemyHealthSystem> OnEnemyKilled;

    public bool IsDestroyed { get; private set; }
    public float MaxHealth { get; private set; }
    public float CurrentRange { get; private set; }

    [HideInInspector]
    public int totalCostSpent { get; private set; }

    private float currentHealth;
    private float currentArmor;
    private float currentDamage;
    private float currentAttackSpeed;
    private float currentCritChance;
    private float currentCritDamage;
    private float currentArmorPenetration;
    [HideInInspector] public float attackSpeedMultiplier = 1f;

    private List<TowerBehavior> activeBehaviors = new List<TowerBehavior>();

    // Tracking universal dos nÃ­veis
    public int[] currentPathLevels { get; private set; } = new int[3] { 0, 0, 0 };

    private Transform targetEnemy;
    public Transform TargetEnemy => targetEnemy;

    private float fireCountdown = 0f;

    private TowerAbilitySystem abilitySystem;
    private NetworkObject networkObject;
    private NetworkedBuilding networkedBuilding;
    private DragonPatrolBehavior dragonPatrol;
    private Coroutine registerWithBuildManagerRoutine;

    void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        networkedBuilding = GetComponent<ExoBeasts.Multiplayer.Sync.NetworkedBuilding>();
        dragonPatrol = GetComponent<DragonPatrolBehavior>();
    }

    void Start()
    {
        if (towerData == null)
        {
            this.enabled = false;
            return;
        }

        abilitySystem = GetComponent<TowerAbilitySystem>();

        CloneBaseStats();
        SetupTowerMode();

        StartCoroutine(SpawnMaterializationFlow());

        // Removemos UpdateAbilities do Start pois os nÃ­veis comeÃ§am zerados ou definidos pelo AbilitySystem
        if (HasCombatAuthority())
            InvokeRepeating("UpdateTarget", 0f, 0.5f);
        RegisterWithBuildManagerIfRuntime();

        if (networkedBuilding != null)
            networkedBuilding.RefreshVisualState();
    }

    void SetupTowerMode()
    {
        if (animator == null) animator = GetComponentInChildren<Animator>();

        if (animator != null)
        {
            animator.SetBool(towerModeBool, true);
        }

        CharacterController cc = GetComponentInChildren<CharacterController>();
        if (cc != null) cc.enabled = false;

        Rigidbody rb = GetComponentInChildren<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }
    }

    void CloneBaseStats()
    {
        MaxHealth = towerData.maxHealth;
        currentHealth = MaxHealth;
        totalCostSpent = towerData.cost;

        currentArmor = towerData.armor;
        currentDamage = towerData.damage;
        currentAttackSpeed = towerData.attackSpeed;
        currentCritChance = towerData.critChance;
        currentCritDamage = towerData.critDamage;
        currentArmorPenetration = towerData.armorPenetration;

        CurrentRange = towerData.attackRange;
        IsDestroyed = false;
        TargetsFlyingEnemies = false;
        attackSpeedMultiplier = 1f;
    }

    // --- MUDANÃ‡A IMPORTANTE AQUI ---
    // Adicionei o parametro 'TowerPath path' para sabermos qual caminho estÃ¡ sendo upado
    public void ApplyUpgrade(Upgrade upgradeToApply, int geoditeCost, int darkEtherCost, TowerPath path)
    {
        if (upgradeToApply == null) return;

        totalCostSpent += geoditeCost;
        totalCostSpent += darkEtherCost;

        // 1. Aplica Modificadores de Stats (Dano, Range, etc)
        foreach (var modifier in upgradeToApply.modifiers)
        {
            ApplyModifier(modifier);
        }

        // 2. Desbloqueia Comportamentos Extras (Se houver)
        if (upgradeToApply.behaviorToUnlock != null)
        {
            GameObject behaviorObject = Instantiate(upgradeToApply.behaviorToUnlock, transform);
            TowerBehavior newBehavior = behaviorObject.GetComponent<TowerBehavior>();
            if (newBehavior != null)
            {
                newBehavior.Initialize(this);
                activeBehaviors.Add(newBehavior);
            }
            else
            {
                Debug.LogError($"[Upgrade] ERRO: Prefab '{upgradeToApply.behaviorToUnlock.name}' NAO tem TowerBehavior na raiz!");
            }
        }

        // 3. Incrementa o nivel na base da torre para UI funcionar
        if (path == TowerPath.DPS) currentPathLevels[0]++;
        else if (path == TowerPath.Control) currentPathLevels[1]++;
        else if (path == TowerPath.Support) currentPathLevels[2]++;
    }

    public void ApplyNetworkUpgradeState(int dpsLevel, int controlLevel, int supportLevel, int syncedTotalCostSpent)
    {
        if (towerData == null) return;

        RebuildFromBaseStats();
        ReplayUpgradePath(TowerPath.DPS, dpsLevel);
        ReplayUpgradePath(TowerPath.Control, controlLevel);
        ReplayUpgradePath(TowerPath.Support, supportLevel);
        totalCostSpent = Mathf.Max(towerData.cost, syncedTotalCostSpent);
    }

    public int TotalCostSpent => totalCostSpent;
    public float CurrentHealth => currentHealth;

    private void RebuildFromBaseStats()
    {
        foreach (TowerBehavior behavior in activeBehaviors)
        {
            if (behavior != null)
                Destroy(behavior.gameObject);
        }

        activeBehaviors.Clear();
        currentPathLevels = new int[3] { 0, 0, 0 };
        CloneBaseStats();
    }

    private void ReplayUpgradePath(TowerPath path, int levelCount)
    {
        int pathIndex = GetPathIndex(path);
        if (pathIndex < 0 || towerData.upgradePaths == null || pathIndex >= towerData.upgradePaths.Count)
            return;

        UpgradePath upgradePath = towerData.upgradePaths[pathIndex];
        if (upgradePath == null || upgradePath.upgradesInPath == null)
            return;

        int safeLevelCount = Mathf.Min(levelCount, upgradePath.upgradesInPath.Count);
        for (int i = 0; i < safeLevelCount; i++)
            ApplyUpgrade(upgradePath.upgradesInPath[i], 0, 0, path);
    }

    private int GetPathIndex(TowerPath path)
    {
        if (path == TowerPath.DPS) return 0;
        if (path == TowerPath.Control) return 1;
        if (path == TowerPath.Support) return 2;
        return -1;
    }

    private void ApplyModifier(StatModifier modifier)
    {
        float value = modifier.value;
        switch (modifier.statToModify)
        {
            case StatType.Damage:
                currentDamage = (modifier.modType == ModificationType.Additive) ? currentDamage + value : currentDamage * (1 + value);
                break;
            case StatType.AttackSpeed:
                currentAttackSpeed = (modifier.modType == ModificationType.Additive) ? currentAttackSpeed + value : currentAttackSpeed * (1 + value);
                break;
            case StatType.Range:
                CurrentRange = (modifier.modType == ModificationType.Additive) ? CurrentRange + value : CurrentRange * (1 + value);
                break;
            case StatType.Armor:
                currentArmor = Mathf.Clamp01(currentArmor + value);
                break;
            case StatType.CritChance:
                currentCritChance = Mathf.Clamp01(currentCritChance + value);
                break;
            case StatType.CritDamage:
                currentCritDamage = (modifier.modType == ModificationType.Additive) ? currentCritDamage + value : currentCritDamage * (1 + value);
                break;
            case StatType.ArmorPenetration:
                currentArmorPenetration = Mathf.Clamp01(currentArmorPenetration + value);
                break;
        }
    }

    void Update()
    {
        if (!HasCombatAuthority()) return;
        if (IsDestroyed) return;
        if (isMaterializing) return;
        if (targetEnemy == null) return;
        if (!IsTargetAllowedByMovementLeash(targetEnemy))
        {
            targetEnemy = null;
            return;
        }

        if (partToRotate != null) RotateTowardsTarget();

        fireCountdown -= Time.deltaTime;
        if (fireCountdown <= 0f)
        {
            float effectiveAttackSpeed = currentAttackSpeed * attackSpeedMultiplier;
            fireCountdown = effectiveAttackSpeed > 0f ? 1f / effectiveAttackSpeed : 99999f;
            Shoot();
        }
    }

    public void Shoot()
    {
        if (!HasCombatAuthority()) return;
        if (isMaterializing) return;
        if (targetEnemy == null) return;
        if (!IsTargetAllowedByMovementLeash(targetEnemy))
        {
            targetEnemy = null;
            return;
        }

        Vector3 originPoint = firePoint != null ? firePoint.position : (partToRotate != null ? partToRotate.position : transform.position);
        Vector3 visualEndPoint = GetTargetHitPoint(targetEnemy, originPoint);

        PiercingBehavior piercer = GetComponent<PiercingBehavior>();
        if (piercer != null)
        {
            Vector3 dir = (targetEnemy.position - originPoint).normalized;
            RaycastHit[] hits = Physics.SphereCastAll(originPoint, 1f, dir, CurrentRange);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            
            int hitsDone = 0;
            int maxHits = 1 + piercer.enemiesToPierce;
            HashSet<EnemyHealthSystem> processed = new HashSet<EnemyHealthSystem>();

            EnemyHealthSystem primary = ResolveEnemyHealth(targetEnemy);
            if (primary != null && !primary.isDead)
            {
                ProcessDamageInstance(primary);
                hitsDone++;
                processed.Add(primary);
            }

            Vector3 finalHitPosition = visualEndPoint;

            foreach (var hit in hits)
            {
                if (hitsDone >= maxHits) break;
                EnemyHealthSystem ehs = hit.collider.GetComponentInParent<EnemyHealthSystem>();
                if (ehs == null) ehs = hit.collider.GetComponent<EnemyHealthSystem>();

                if (ehs != null && !processed.Contains(ehs) && !ehs.isDead && IsTargetAllowedByMovementLeash(ehs.transform))
                {
                    ProcessDamageInstance(ehs);
                    hitsDone++;
                    processed.Add(ehs);
                    finalHitPosition = GetTargetHitPoint(ehs.transform, originPoint);
                }
            }

            PlayAttackVisualForObservers(originPoint, finalHitPosition);
        }
        else
        {
            EnemyHealthSystem healthSystem = ResolveEnemyHealth(targetEnemy);
            if (healthSystem != null) 
            {
                ProcessDamageInstance(healthSystem);
                PlayAttackVisualForObservers(originPoint, GetTargetHitPoint(targetEnemy, originPoint));
            }
        }
    }

    public void PlayAttackVisualLocal(Vector3 originPoint, Vector3 endPoint)
    {
        if (animator == null)
            animator = GetComponentInChildren<Animator>();

        if (partToRotate != null)
            RotatePartTowards(endPoint);

        if (playAttackAnimation && animator != null)
            animator.SetTrigger(shootTrigger);

        TowerTracerVFX tracer = GetComponentInChildren<TowerTracerVFX>(true);
        if (tracer != null)
            tracer.DrawTracer(originPoint, endPoint);

        if (!string.IsNullOrEmpty(somTiro))
        {
            RuntimeManager.PlayOneShot(somTiro, originPoint);
        }
    }

    private void PlayAttackVisualForObservers(Vector3 originPoint, Vector3 endPoint)
    {
        PlayAttackVisualLocal(originPoint, endPoint);

        if (networkedBuilding != null &&
            NetworkManager.Singleton != null &&
            NetworkManager.Singleton.IsListening &&
            NetworkManager.Singleton.IsServer)
        {
            networkedBuilding.BroadcastTowerAttackVisualClientRpc(originPoint, endPoint);
        }
    }

    private void ProcessDamageInstance(EnemyHealthSystem healthSystem)
    {
        if (!HasCombatAuthority()) return;
        if (healthSystem == null) return;
        float damageToDeal = currentDamage;
        bool isCritical = UnityEngine.Random.value <= currentCritChance;

        if (isCritical) damageToDeal *= currentCritDamage;

        if (OnCalculateDamage != null)
        {
            foreach (Func<EnemyHealthSystem, float, float> modifier in OnCalculateDamage.GetInvocationList())
            {
                damageToDeal = modifier(healthSystem, damageToDeal);
            }
        }

        ulong attackerClientId = NetworkManager.ServerClientId;
        PlayerHealthSystem attackerHealth = null;
        NetworkGameplayResolver.TryResolveAttackerFromBuilding(this, out attackerClientId, out attackerHealth);

        // Torres marcam IsFromTower = true para poder quebrar escudos de inimigos
        DamageContext damageContext = new DamageContext(attackerClientId, isCritical, DamageFeedbackMode.AllObservers, isFromTower: true);

        bool enemyDied = healthSystem.ApplyAuthoritativeDamage(
            damageToDeal,
            currentArmorPenetration,
            damageContext,
            attackerHealth);

        if (enemyDied) OnEnemyKilled?.Invoke(healthSystem);
        OnTargetDamaged?.Invoke(healthSystem);
        if (isCritical) OnCriticalHit?.Invoke(healthSystem);
    }

    void RotateTowardsTarget()
    {
        if (targetEnemy == null)
            return;

        RotatePartTowards(targetEnemy.position);
    }

    private void RotatePartTowards(Vector3 targetPosition)
    {
        if (partToRotate == null)
            return;

        Vector3 direction = targetPosition - transform.position;
        direction.y = 0;

        if (direction == Vector3.zero) return;

        Quaternion lookRotation = Quaternion.LookRotation(direction);
        Quaternion offsetRotation = Quaternion.Euler(rotationOffset);
        Quaternion finalTargetRotation = lookRotation * offsetRotation;

        Vector3 smoothedRotation = Quaternion.Lerp(partToRotate.rotation, finalTargetRotation, Time.deltaTime * 10f).eulerAngles;
        partToRotate.rotation = Quaternion.Euler(0f, smoothedRotation.y, 0f);
    }

    // Buffer pre-alocado para Physics.OverlapSphereNonAlloc — evita GC a cada UpdateTarget (2x/s/torre).
    // 64 colliders cobre cenarios densos de wave; tamanho fixo evita realocacao.
    private static readonly Collider[] _targetingBuffer = new Collider[64];

    void UpdateTarget()
    {
        if (!HasCombatAuthority())
        {
            targetEnemy = null;
            return;
        }

        Vector3 originPoint = partToRotate != null ? partToRotate.position : transform.position;

        if (TryUpdateTargetFromEnemyRegistry(originPoint))
            return;

        int hitCount = Physics.OverlapSphereNonAlloc(originPoint, CurrentRange, _targetingBuffer);

        Transform nearestEnemy = null;
        float shortestDistance = Mathf.Infinity;

        for (int i = 0; i < hitCount; i++)
        {
            Collider col = _targetingBuffer[i];
            if (col == null) continue;

            if (col.CompareTag(enemyTag) || (col.transform.parent != null && col.transform.parent.CompareTag(enemyTag)))
            {
                EnemyController enemyController = col.GetComponent<EnemyController>();
                if (enemyController == null) enemyController = col.GetComponentInParent<EnemyController>();

                if (enemyController == null || enemyController.enemyData == null) continue;
                if (!IsTargetAllowedByMovementLeash(enemyController.transform)) continue;

                EnemyType enemyType = enemyController.enemyData.enemyType;

                bool isTargetable = (enemyType == EnemyType.Terrestre) ||
                                    (TargetsFlyingEnemies && enemyType == EnemyType.Voador);

                if (isTargetable)
                {
                    Vector3 closestPointOnEnemy = col.ClosestPoint(originPoint);
                    float distanceToSkin = Vector3.Distance(originPoint, closestPointOnEnemy);

                    if (distanceToSkin < shortestDistance)
                    {
                        shortestDistance = distanceToSkin;
                        nearestEnemy = enemyController.transform;
                    }
                }
            }
        }

        targetEnemy = nearestEnemy;
    }

    private bool TryUpdateTargetFromEnemyRegistry(Vector3 originPoint)
    {
        // OPTIMIZATION (Sprint 3 / Item E3p2 - 2026-05-08): usa o registry do
        // HordeManager no servidor/host e mantem Physics como fallback em clientes.
        IReadOnlyList<EnemyController> enemies = HordeManager.GetActiveEnemies();
        if (enemies == null || enemies.Count == 0)
            return false;

        Transform nearestEnemy = null;
        float shortestDistance = Mathf.Infinity;

        for (int i = 0; i < enemies.Count; i++)
        {
            EnemyController enemyController = enemies[i];
            if (enemyController == null || enemyController.IsDead || enemyController.enemyData == null)
                continue;
            if (!IsTargetAllowedByMovementLeash(enemyController.transform))
                continue;

            EnemyType enemyType = enemyController.enemyData.enemyType;
            bool isTargetable = (enemyType == EnemyType.Terrestre) ||
                                (TargetsFlyingEnemies && enemyType == EnemyType.Voador);

            if (!isTargetable)
                continue;

            Collider enemyCol = enemyController.GetComponentInChildren<Collider>();
            Vector3 closestPointOnEnemy = enemyCol != null
                ? enemyCol.ClosestPoint(originPoint)
                : enemyController.transform.position;
            float distanceToSkin = Vector3.Distance(originPoint, closestPointOnEnemy);

            if (distanceToSkin > CurrentRange || distanceToSkin >= shortestDistance)
                continue;

            shortestDistance = distanceToSkin;
            nearestEnemy = enemyController.transform;
        }

        targetEnemy = nearestEnemy;
        return true;
    }

    private bool IsTargetAllowedByMovementLeash(Transform candidate)
    {
        if (candidate == null)
            return false;

        if (dragonPatrol == null)
            return true;

        return dragonPatrol.IsTargetInsidePatrolLeash(candidate);
    }

    private EnemyHealthSystem ResolveEnemyHealth(Transform candidate)
    {
        if (candidate == null)
            return null;

        EnemyHealthSystem healthSystem = candidate.GetComponent<EnemyHealthSystem>();
        if (healthSystem == null)
            healthSystem = candidate.GetComponentInParent<EnemyHealthSystem>();
        if (healthSystem == null)
            healthSystem = candidate.GetComponentInChildren<EnemyHealthSystem>();

        return healthSystem;
    }

    private Vector3 GetTargetHitPoint(Transform candidate, Vector3 originPoint)
    {
        if (candidate == null)
            return originPoint;

        Collider enemyCol = candidate.GetComponentInChildren<Collider>();
        return enemyCol != null ? enemyCol.ClosestPoint(originPoint) : candidate.position;
    }

    public void SellTower(float refundPercentage)
    {
        if (CurrencyManager.Instance != null)
        {
            int refundAmount = Mathf.FloorToInt(totalCostSpent * refundPercentage);
            CurrencyManager.Instance.AddCurrency(refundAmount, CurrencyType.Geodites);
        }
        DestroyTower();
    }

    [HideInInspector] public bool IsInvulnerable = false;
    public delegate void DamageEvent(float damage, Transform attacker);
    public event DamageEvent OnDamageTaken;
    public delegate bool FatalDamageEvent();
    public event FatalDamageEvent OnFatalDamagePrevented;

    public void AddMaxHealth(float amount)
    {
        MaxHealth += amount;
        if (MaxHealth < 1) MaxHealth = 1;
        currentHealth += amount;
    }

    public void TakeDamage(float amount, Transform attacker = null)
    {
        if (!HasCombatAuthority()) return;
        if (IsDestroyed || IsInvulnerable) return;
        float remainingDamage = amount;

        Collider[] colliders = Physics.OverlapSphere(transform.position, 5f);
        foreach (var col in colliders)
        {
            SpiritualBarrierBehavior barrier = col.GetComponent<SpiritualBarrierBehavior>();
            if (barrier != null && barrier.towerController != this)
            {
                remainingDamage = barrier.AbsorbDamage(remainingDamage);
            }
        }

        AllyShield shield = GetComponent<AllyShield>();
        if (shield != null && shield.IsActive)
        {
            remainingDamage = shield.AbsorbDamage(remainingDamage);
        }

        DragonAuraBuff auraBuff = GetComponent<DragonAuraBuff>();
        float dmgRed = auraBuff != null ? auraBuff.DamageReduction : 0f;

        float finalDamage = remainingDamage * (1 - currentArmor) * (1 - dmgRed);
        currentHealth -= finalDamage;
        
        OnDamageTaken?.Invoke(finalDamage, attacker);

        if (currentHealth <= 0)
        {
            if (OnFatalDamagePrevented != null && OnFatalDamagePrevented.Invoke())
            {
                currentHealth = 1f; // Preveniu a morte
            }
            else
            {
                currentHealth = 0;
                DestroyTower(isKilledInCombat: true);
            }
        }
    }

    private void DestroyTower(bool isKilledInCombat = false)
    {
        IsDestroyed = true;
        targetEnemy = null;

        // Notifica o pop-up apenas quando a torre é destruída em combate (não quando vendida).
        if (isKilledInCombat && towerData != null)
            JuiceEvents.OnTowerDied?.Invoke(towerData.name);

        if (TowerSelectionManager.Instance != null)
        {
            TowerSelectionManager.Instance.DeselectAll();
        }
        // Ao invés de Destruir o GameObject (que quebra os scripts de Reviver), nós apenas escondemos a torre visualmente
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) 
        {
            r.enabled = false;
        }
    }

    public void Revive(float healthPercentage)
    {
        if (!IsDestroyed) return;
        IsDestroyed = false;
        currentHealth = MaxHealth * healthPercentage;
        
        // Reativa a torre visualmente
        foreach (Renderer r in GetComponentsInChildren<Renderer>()) 
        {
            r.enabled = true;
        }

        StartCoroutine(SpawnMaterializationFlow());
    }

    private System.Collections.IEnumerator SpawnMaterializationFlow()
    {
        // Passo A: Bloqueio
        isMaterializing = true;
        
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>();
        System.Collections.Generic.List<Renderer> targetRenderers = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in allRenderers)
        {
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
            {
                targetRenderers.Add(r);
            }
        }

        // Passo B: Textura Inicial (Holograma)
        if (materialHolograma != null)
        {
            foreach (Renderer r in targetRenderers)
            {
                r.material = materialHolograma;
            }
        }

        // Passo C e D: AnimaÃ§Ã£o do Shader
        float elapsedTime = 0f;
        while (elapsedTime < tempoDeSpawn)
        {
            elapsedTime += Time.deltaTime;
            float progress = Mathf.Clamp01(elapsedTime / tempoDeSpawn);
            
            if (materialHolograma != null)
            {
                foreach (Renderer r in targetRenderers)
                {
                    if (r.material.HasProperty("Proguesso_Holograma"))
                    {
                        r.material.SetFloat("Proguesso_Holograma", progress);
                    }
                }
            }
            yield return null;
        }

        // Passo E: Materiais Finais
        if (materialToon != null && materialOutline != null)
        {
            Material[] finalMaterials = new Material[] { materialToon, materialOutline };
            foreach (Renderer r in targetRenderers)
            {
                r.materials = finalMaterials;
            }
        }

        // Passo F: LiberaÃ§Ã£o
        isMaterializing = false;
    }

    public void Heal(float amount) { currentHealth = Mathf.Min(currentHealth + amount, MaxHealth); }
    public void AddArmorBonus(float amount) { currentArmor += amount; }
    public void AddAttackSpeedBonus(float amount) 
    { 
        if (amount >= 0) currentAttackSpeed *= (1 + amount); 
        else currentAttackSpeed /= (1 + Mathf.Abs(amount)); 
    }
    public void AddDamageBonus(float amount) 
    { 
        if (amount >= 0) currentDamage *= (1 + amount); 
        else currentDamage /= (1 + Mathf.Abs(amount)); 
    }
    public float CurrentDamage => currentDamage;

    public void AddRangeBonus(float amount)
    {
        if (amount >= 0) CurrentRange *= (1 + amount);
        else CurrentRange /= (1 + Mathf.Abs(amount));
    }
    public void PerformExtraAttack() { Shoot(); }

    public void FireExtraProjectileAt(EnemyHealthSystem target, float damagePercent)
    {
        if (!HasCombatAuthority()) return;
        if (target == null) return;
        float damageToDeal = currentDamage * damagePercent;
        
        bool isCritical = UnityEngine.Random.value <= currentCritChance;
        if (isCritical) damageToDeal *= currentCritDamage;

        if (OnCalculateDamage != null)
        {
            foreach (Func<EnemyHealthSystem, float, float> modifier in OnCalculateDamage.GetInvocationList())
            {
                damageToDeal = modifier(target, damageToDeal);
            }
        }

        ulong attackerClientId = NetworkManager.ServerClientId;
        PlayerHealthSystem attackerHealth = null;
        NetworkGameplayResolver.TryResolveAttackerFromBuilding(this, out attackerClientId, out attackerHealth);

        // Torres marcam IsFromTower = true para poder quebrar escudos de inimigos
        DamageContext damageContext = new DamageContext(attackerClientId, isCritical, DamageFeedbackMode.AllObservers, isFromTower: true);

        bool enemyDied = target.ApplyAuthoritativeDamage(
            damageToDeal,
            currentArmorPenetration,
            damageContext,
            attackerHealth);
        if (enemyDied) OnEnemyKilled?.Invoke(target);
        if (isCritical) OnCriticalHit?.Invoke(target);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, CurrentRange);
    }

    private void RegisterWithBuildManagerIfRuntime()
    {
        if (networkObject != null && !networkObject.IsSpawned)
            return;

        RegisterWithBuildManager();
    }

    private void RegisterWithBuildManager()
    {
        if (BuildManager.Instance != null)
        {
            BuildManager.Instance.RegisterTower(this);
            return;
        }

        if (registerWithBuildManagerRoutine == null)
            registerWithBuildManagerRoutine = StartCoroutine(RegisterWithBuildManagerWhenReady());
    }

    private System.Collections.IEnumerator RegisterWithBuildManagerWhenReady()
    {
        while (BuildManager.Instance == null)
            yield return null;

        BuildManager.Instance.RegisterTower(this);
        registerWithBuildManagerRoutine = null;
    }

    private void UnregisterFromBuildManager()
    {
        if (registerWithBuildManagerRoutine != null)
        {
            StopCoroutine(registerWithBuildManagerRoutine);
            registerWithBuildManagerRoutine = null;
        }

        if (BuildManager.Instance != null)
            BuildManager.Instance.UnregisterTower(this);
    }

    private void OnDestroy()
    {
        UnregisterFromBuildManager();
    }

    private bool HasCombatAuthority()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return true;

        return NetworkManager.Singleton.IsServer;
    }
}
