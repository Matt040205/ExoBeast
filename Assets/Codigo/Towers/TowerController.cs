using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;
using Unity.Netcode;
using ExoBeasts.Multiplayer.Sync;

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

    [Header("Visual e AnimaÃ§Ã£o")]
    public Animator animator;
    public string shootTrigger = "Attack";
    public string towerModeBool = "IsTower";
    public Vector3 rotationOffset;

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

    private List<TowerBehavior> activeBehaviors = new List<TowerBehavior>();

    // Tracking universal dos nÃ­veis
    public int[] currentPathLevels { get; private set; } = new int[3] { 0, 0, 0 };

    private Transform targetEnemy;
    public Transform TargetEnemy => targetEnemy;

    private float fireCountdown = 0f;

    private TowerAbilitySystem abilitySystem;
    private NetworkObject networkObject;
    private NetworkedBuilding networkedBuilding;

    void Awake()
    {
        networkObject = GetComponent<NetworkObject>();
        networkedBuilding = GetComponent<ExoBeasts.Multiplayer.Sync.NetworkedBuilding>();
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
        InvokeRepeating("UpdateTarget", 0f, 0.5f);

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
            GameObject behaviorObject = Instantiate(upgradeToApply.behaviorToUnlock.gameObject, transform);
            TowerBehavior newBehavior = behaviorObject.GetComponent<TowerBehavior>();
            if (newBehavior != null)
            {
                newBehavior.Initialize(this);
                activeBehaviors.Add(newBehavior);
            }
        }

        // 3. Incrementa o nÃ­vel na base da torre para UI funcionar
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
        if (IsDestroyed) return;
        if (isMaterializing) return;
        if (targetEnemy == null) return;

        if (partToRotate != null) RotateTowardsTarget();

        fireCountdown -= Time.deltaTime;
        if (fireCountdown <= 0f)
        {
            fireCountdown = 1f / currentAttackSpeed;
            Shoot();
        }
    }

    public void Shoot()
    {
        if (isMaterializing) return;
        if (targetEnemy == null) return;
        if (animator != null) animator.SetTrigger(shootTrigger);

        Vector3 originPoint = firePoint != null ? firePoint.position : (partToRotate != null ? partToRotate.position : transform.position);
        TowerTracerVFX tracer = GetComponentInChildren<TowerTracerVFX>();

        PiercingBehavior piercer = GetComponent<PiercingBehavior>();
        if (piercer != null)
        {
            Vector3 dir = (targetEnemy.position - originPoint).normalized;
            RaycastHit[] hits = Physics.SphereCastAll(originPoint, 1f, dir, CurrentRange);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));
            
            int hitsDone = 0;
            int maxHits = 1 + piercer.enemiesToPierce;
            HashSet<EnemyHealthSystem> processed = new HashSet<EnemyHealthSystem>();

            EnemyHealthSystem primary = targetEnemy.GetComponent<EnemyHealthSystem>();
            if (primary != null)
            {
                ProcessDamageInstance(primary);
                hitsDone++;
                processed.Add(primary);
            }

            Vector3 finalHitPosition = targetEnemy.position;

            foreach (var hit in hits)
            {
                if (hitsDone >= maxHits) break;
                EnemyHealthSystem ehs = hit.collider.GetComponentInParent<EnemyHealthSystem>();
                if (ehs == null) ehs = hit.collider.GetComponent<EnemyHealthSystem>();

                if (ehs != null && !processed.Contains(ehs) && !ehs.isDead)
                {
                    ProcessDamageInstance(ehs);
                    hitsDone++;
                    processed.Add(ehs);
                    finalHitPosition = ehs.transform.position;
                }
            }

            if (tracer != null)
            {
                tracer.DrawTracer(originPoint, finalHitPosition);
            }
        }
        else
        {
            EnemyHealthSystem healthSystem = targetEnemy.GetComponent<EnemyHealthSystem>();
            if (healthSystem != null) 
            {
                ProcessDamageInstance(healthSystem);

                if (tracer != null)
                {
                    // Usa ClosestPoint para que o rastro bata na borda do inimigo e não atravesse até o centro
                    Collider enemyCol = targetEnemy.GetComponentInChildren<Collider>();
                    Vector3 endPoint = enemyCol != null ? enemyCol.ClosestPoint(originPoint) : targetEnemy.position;
                    
                    tracer.DrawTracer(originPoint, endPoint);
                }
            }
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

        bool enemyDied = healthSystem.ApplyAuthoritativeDamage(
            damageToDeal,
            currentArmorPenetration,
            isCritical,
            attackerClientId,
            attackerHealth);

        if (enemyDied) OnEnemyKilled?.Invoke(healthSystem);
        OnTargetDamaged?.Invoke(healthSystem);
        if (isCritical) OnCriticalHit?.Invoke(healthSystem);
    }

    void RotateTowardsTarget()
    {
        Vector3 direction = targetEnemy.position - transform.position;
        direction.y = 0;

        if (direction == Vector3.zero) return;

        Quaternion lookRotation = Quaternion.LookRotation(direction);
        Quaternion offsetRotation = Quaternion.Euler(rotationOffset);
        Quaternion finalTargetRotation = lookRotation * offsetRotation;

        Vector3 smoothedRotation = Quaternion.Lerp(partToRotate.rotation, finalTargetRotation, Time.deltaTime * 10f).eulerAngles;
        partToRotate.rotation = Quaternion.Euler(0f, smoothedRotation.y, 0f);
    }

    void UpdateTarget()
    {
        Vector3 originPoint = partToRotate != null ? partToRotate.position : transform.position;

        Collider[] collidersInRadius = Physics.OverlapSphere(originPoint, CurrentRange);

        Transform nearestEnemy = null;
        float shortestDistance = Mathf.Infinity;

        foreach (Collider col in collidersInRadius)
        {
            if (col.CompareTag(enemyTag) || (col.transform.parent != null && col.transform.parent.CompareTag(enemyTag)))
            {
                EnemyController enemyController = col.GetComponent<EnemyController>();
                if (enemyController == null) enemyController = col.GetComponentInParent<EnemyController>();

                if (enemyController == null || enemyController.enemyData == null) continue;

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
                        nearestEnemy = col.transform;
                    }
                }
            }
        }

        targetEnemy = nearestEnemy;
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
                DestroyTower();
            }
        }
    }

    private void DestroyTower()
    {
        IsDestroyed = true;
        targetEnemy = null;

        if (TowerSelectionManager.Instance != null)
        {
            TowerSelectionManager.Instance.DeselectAll();
        }
        // Ao invÃ©s de Destruir o GameObject (que quebra os scripts de Reviver), nÃ³s apenas escondemos a torre visualmente
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

        bool enemyDied = target.ApplyAuthoritativeDamage(
            damageToDeal,
            currentArmorPenetration,
            isCritical,
            attackerClientId,
            attackerHealth);
        if (enemyDied) OnEnemyKilled?.Invoke(target);
        if (isCritical) OnCriticalHit?.Invoke(target);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, CurrentRange);
    }

    private bool HasCombatAuthority()
    {
        if (networkObject == null || !networkObject.IsSpawned || NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return true;

        return NetworkManager.Singleton.IsServer;
    }
}
