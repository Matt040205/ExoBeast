using UnityEngine;
using System;
using System.Linq;
using System.Collections.Generic;

public class TowerController : MonoBehaviour
{
    [Header("Referências Principais")]
    public CharacterBase towerData;
    public Transform partToRotate;
    public Transform firePoint;

    [Header("Visual e Animação")]
    public Animator animator;
    public string shootTrigger = "Attack";
    public string towerModeBool = "IsTower";
    public Vector3 rotationOffset;

    [Header("Configurações de IA")]
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

    // REMOVIDO: currentPathLevels (Quem cuida disso agora é o AbilitySystem)

    private Transform targetEnemy;
    private float fireCountdown = 0f;

    private TowerAbilitySystem abilitySystem;

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

        // Removemos UpdateAbilities do Start pois os níveis começam zerados ou definidos pelo AbilitySystem
        InvokeRepeating("UpdateTarget", 0f, 0.5f);
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

    // --- MUDANÇA IMPORTANTE AQUI ---
    // Adicionei o parametro 'TowerPath path' para sabermos qual caminho está sendo upado
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

        // 3. Atualiza o Sistema de Habilidades Mistas
        if (abilitySystem != null)
        {
            // Tenta converter para PaintAbilitySystem para usar a função nova UpgradePath
            PaintAbilitySystem paintSystem = abilitySystem as PaintAbilitySystem;
            if (paintSystem != null)
            {
                paintSystem.UpgradePath(path);
            }
            else
            {
                // Fallback para outras torres que ainda usem o sistema antigo
                // Se você tiver outras torres, precisará adicionar UpgradePath na base TowerAbilitySystem depois
                Debug.LogWarning("AbilitySystem não é PaintAbilitySystem. O nível misto pode não funcionar.");
            }
        }
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
        if (targetEnemy == null) return;

        if (animator != null)
        {
            animator.SetTrigger(shootTrigger);
        }

        EnemyHealthSystem healthSystem = targetEnemy.GetComponent<EnemyHealthSystem>();

        if (healthSystem != null)
        {
            float damageToDeal = currentDamage;
            bool isCritical = UnityEngine.Random.value <= currentCritChance;

            if (isCritical)
            {
                damageToDeal *= currentCritDamage;
            }

            if (OnCalculateDamage != null)
            {
                foreach (Func<EnemyHealthSystem, float, float> modifier in OnCalculateDamage.GetInvocationList())
                {
                    damageToDeal = modifier(healthSystem, damageToDeal);
                }
            }

            bool enemyDied = healthSystem.TakeDamage(damageToDeal, currentArmorPenetration);

            if (enemyDied)
            {
                OnEnemyKilled?.Invoke(healthSystem);
            }

            // Isso aciona o PaintAbilitySystem.OnHit()
            OnTargetDamaged?.Invoke(healthSystem);

            if (isCritical)
            {
                OnCriticalHit?.Invoke(healthSystem);
            }
        }
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

    public void TakeDamage(float amount)
    {
        if (IsDestroyed) return;
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

        float finalDamage = remainingDamage * (1 - currentArmor);
        currentHealth -= finalDamage;

        if (currentHealth <= 0)
        {
            currentHealth = 0;
            DestroyTower();
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
        Destroy(gameObject);
    }

    public void Revive(float healthPercentage)
    {
        if (!IsDestroyed) return;
        IsDestroyed = false;
        currentHealth = MaxHealth * healthPercentage;
    }

    public void Heal(float amount) { currentHealth = Mathf.Min(currentHealth + amount, MaxHealth); }
    public void AddArmorBonus(float amount) { currentArmor += amount; }
    public void AddAttackSpeedBonus(float amount) { currentAttackSpeed *= (1 + amount); }
    public void AddDamageBonus(float amount) { currentDamage *= (1 + amount); }
    public void PerformExtraAttack() { Shoot(); }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.cyan;
        Gizmos.DrawWireSphere(transform.position, CurrentRange);
    }
}