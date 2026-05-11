using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExoBeasts.Multiplayer.Sync;

public class EnemyHealthSystem : MonoBehaviour
{
    [Header("Referências")]
    public EnemyDataSO enemyData;
    public Material markedMaterial;
    private Renderer enemyRenderer;
    private Renderer[] enemyRenderers;
    private WorldSpaceEnemyUI worldSpaceUI;
    private Material[] originalMaterials;
    private readonly Dictionary<Renderer, Material[]> originalRendererMaterials = new Dictionary<Renderer, Material[]>();

    [Header("Feedback Visual")]
    public GameObject deathVfxPrefab;
    public Transform popupSpawnPoint;
    public Color flashColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    public float flashDuration = 0.05f;
    public string flashAmountProperty = "_FlashAmount";
    public string flashColorProperty = "_FlashColor";

    [Header("Efeito Dissolve")]
    public float dissolveDuration = 2f;
    public string dissolveAmountProperty = "_DissolveAmount";

    [Header("Evento de Morte (Chamavel por Animation Event)")]
    public UnityEngine.Events.UnityEvent OnDeathDissolveStart;

    private Coroutine flashCoroutine;
    private MaterialPropertyBlock propBlock;

    [Header("Status Atual (Servidor Autoritativo)")]
    public float currentHealth;
    public bool isDead;

    private float baseArmor;
    private float currentArmorModifier = 0f;
    private int armorShredStacks = 0;
    private float markedDamageMultiplier = 1f;
    private float vulnerabilityMultiplier = 1f;

    private EnemyController enemyController;
    private NetworkedEnemy networkedEnemy;
    private bool isMarked = false;
    private Coroutine vulnerabilityCoroutine;

    void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        networkedEnemy = GetComponent<NetworkedEnemy>();
        worldSpaceUI = GetComponentInChildren<WorldSpaceEnemyUI>();

        propBlock = new MaterialPropertyBlock();
        CacheRenderers();
    }

    public void InitializeHealth(int level)
    {
        if (enemyData == null) return;

        currentHealth = enemyData.GetHealth(level);
        baseArmor = enemyData.GetArmor(level);
        currentArmorModifier = 0f;
        armorShredStacks = 0;
        isDead = false;
        markedDamageMultiplier = 1f;
        vulnerabilityMultiplier = 1f;

        RestoreOriginalMaterialsOnAllRenderers();
        ClearPropertyBlocksOnAllRenderers();
        isMarked = false;

        if (networkedEnemy != null && networkedEnemy.IsServer)
        {
            networkedEnemy.NetworkHealth.Value = currentHealth;
            networkedEnemy.IsDead.Value = false;
        }
    }

    public bool TakeDamage(float damage, float armorPenetration = 0f, bool isCritical = false, ulong attackerId = 0)
    {
        DamageContext damageContext = new DamageContext(attackerId, isCritical, DamageFeedbackMode.InstigatorOnly);
        return TakeDamageDetailed(damage, armorPenetration, damageContext, out _);
    }

    public bool TakeDamage(float damage, DamageContext damageContext, float armorPenetration = 0f)
    {
        return TakeDamageDetailed(damage, armorPenetration, damageContext, out _);
    }

    public bool ApplyAuthoritativeDamage(
        float damage,
        float armorPenetration,
        bool isCritical,
        ulong attackerId,
        PlayerHealthSystem attackerHealth = null)
    {
        DamageContext damageContext = new DamageContext(attackerId, isCritical, DamageFeedbackMode.InstigatorOnly);
        return ApplyAuthoritativeDamage(damage, armorPenetration, damageContext, attackerHealth);
    }

    public bool ApplyAuthoritativeDamage(
        float damage,
        float armorPenetration,
        DamageContext damageContext,
        PlayerHealthSystem attackerHealth = null)
    {
        return ApplyAuthoritativeDamageDetailed(damage, armorPenetration, damageContext, attackerHealth, out _);
    }

    public bool ApplyAuthoritativeDamageDetailed(
        float damage,
        float armorPenetration,
        DamageContext damageContext,
        PlayerHealthSystem attackerHealth,
        out float finalDamageApplied)
    {
        bool result = TakeDamageDetailed(damage, armorPenetration, damageContext, out finalDamageApplied);

        if (finalDamageApplied > 0f && attackerHealth != null)
            attackerHealth.TriggerDamageDealt(finalDamageApplied);

        return result;
    }

    public bool TakeDamageDetailed(float damage, float armorPenetration, bool isCritical, ulong attackerId, out float finalDamageApplied)
    {
        DamageContext damageContext = new DamageContext(attackerId, isCritical, DamageFeedbackMode.InstigatorOnly);
        return TakeDamageDetailed(damage, armorPenetration, damageContext, out finalDamageApplied);
    }

    public bool TakeDamageDetailed(float damage, float armorPenetration, DamageContext damageContext, out float finalDamageApplied)
    {
        finalDamageApplied = 0f;

        if (networkedEnemy != null && networkedEnemy.IsSpawned && !networkedEnemy.IsServer) return false;
        if (isDead) return false;

        float damageWithMark = damage * markedDamageMultiplier * vulnerabilityMultiplier;
        float armorToIgnore = baseArmor * armorPenetration;
        float effectiveArmor = Mathf.Max(0, baseArmor - currentArmorModifier - armorToIgnore);
        float finalDamage = damageWithMark * (1.0f - Mathf.Clamp01(effectiveArmor / 100f));
        if (finalDamage < 0) finalDamage = 0;
        finalDamageApplied = finalDamage;

        currentHealth -= finalDamage;

        if (networkedEnemy != null)
        {
            networkedEnemy.NetworkHealth.Value = currentHealth;
            if (finalDamage > 0)
                networkedEnemy.TriggerHitVisual(finalDamage, damageContext);
        }

        if (currentHealth <= 0)
        {
            Die();
            return true;
        }
        return false;
    }

    private float currentFlashTime = 0f;

    public void ShowHitVisualLocal(float damageAmount, bool isCritical, bool showPopup)
    {
        currentFlashTime = flashDuration;
        if (flashCoroutine == null)
        {
            flashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        if (showPopup)
        {
            SpawnDamagePopupLocal((int)damageAmount, isCritical);
        }
    }

    private IEnumerator HitFlashRoutine()
    {
        if (HasCachedRenderers())
        {
            foreach (Renderer renderer in enemyRenderers)
            {
                if (renderer != null)
                {
                    renderer.GetPropertyBlock(propBlock);
                    propBlock.SetFloat(flashAmountProperty, 1f);
                    propBlock.SetColor(flashColorProperty, flashColor);
                    renderer.SetPropertyBlock(propBlock);
                }
            }
        }

        while (currentFlashTime > 0f)
        {
            currentFlashTime -= Time.deltaTime;
            yield return null;
        }

        if (HasCachedRenderers())
        {
            foreach (Renderer renderer in enemyRenderers)
            {
                if (renderer != null)
                {
                    renderer.GetPropertyBlock(propBlock);
                    propBlock.SetFloat(flashAmountProperty, 0f);
                    renderer.SetPropertyBlock(propBlock);
                }
            }
        }

        flashCoroutine = null;
    }

    private void SpawnDamagePopupLocal(int damageAmount, bool isCritical)
    {
        Vector3 spawnPos = popupSpawnPoint != null ? popupSpawnPoint.position : transform.position + Vector3.up * 1.5f;
        if (UIPoolManager.Instance != null)
            UIPoolManager.Instance.SpawnDamagePopup(spawnPos, damageAmount, isCritical);
    }

    public void ApplyArmorShred(float percentage, int maxStacks)
    {
        if (networkedEnemy != null && !networkedEnemy.IsServer) return;
        if (armorShredStacks < maxStacks)
        {
            armorShredStacks++;
            currentArmorModifier += percentage;
        }
    }

    public void ApplyTemporaryArmorShred(float percentage, float duration)
    {
        if (networkedEnemy != null && !networkedEnemy.IsServer) return;
        StartCoroutine(TemporaryArmorShredRoutine(percentage, duration));
    }

    private System.Collections.IEnumerator TemporaryArmorShredRoutine(float percentage, float duration)
    {
        float actualArmorVal = baseArmor * percentage;
        currentArmorModifier += actualArmorVal;
        yield return new WaitForSeconds(duration);
        currentArmorModifier -= actualArmorVal;
    }

    private Coroutine markCoroutine;
    public void ApplyMarkedStatus(float multiplier, float duration = 0f)
    {
        if (networkedEnemy != null && !networkedEnemy.IsServer) return;
        markedDamageMultiplier = multiplier;

        bool shouldMark = multiplier > 1.0f;
        ApplyMarkedVisualLocal(shouldMark);

        if (networkedEnemy != null && networkedEnemy.IsSpawned)
            networkedEnemy.ApplyMarkVisualClientRpc(shouldMark);

        if (shouldMark && duration > 0)
        {
            if (markCoroutine != null) StopCoroutine(markCoroutine);
            markCoroutine = StartCoroutine(MarkExpirationRoutine(duration));
        }
    }

    public void ApplyMarkedVisualLocal(bool marked)
    {
        if (!HasCachedRenderers()) return;

        if (marked && markedMaterial != null)
        {
            ApplyMaterialToAllRenderers(markedMaterial);
            isMarked = true;
        }
        else if (!marked && isMarked)
        {
            RestoreOriginalMaterialsOnAllRenderers();
            isMarked = false;
        }
    }

    private void CacheRenderers()
    {
        enemyRenderer = GetComponent<Renderer>();
        enemyRenderers = GetComponentsInChildren<Renderer>(true);

        if ((enemyRenderers == null || enemyRenderers.Length == 0) && enemyRenderer != null)
            enemyRenderers = new[] { enemyRenderer };

        if (enemyRenderer == null && enemyRenderers != null && enemyRenderers.Length > 0)
            enemyRenderer = enemyRenderers[0];

        originalRendererMaterials.Clear();
        if (enemyRenderers != null)
        {
            foreach (Renderer renderer in enemyRenderers)
            {
                if (renderer == null || originalRendererMaterials.ContainsKey(renderer))
                    continue;

                originalRendererMaterials.Add(renderer, renderer.sharedMaterials.ToArray());
            }
        }

        if (enemyRenderer != null && originalMaterials == null)
            originalMaterials = enemyRenderer.sharedMaterials.ToArray();
    }

    private bool HasCachedRenderers()
    {
        if (enemyRenderers == null || enemyRenderers.Length == 0 || originalRendererMaterials.Count == 0)
            CacheRenderers();

        return enemyRenderers != null && enemyRenderers.Length > 0 && originalRendererMaterials.Count > 0;
    }

    private void ApplyMaterialToAllRenderers(Material material)
    {
        if (material == null || !HasCachedRenderers())
            return;

        foreach (Renderer renderer in enemyRenderers)
        {
            if (renderer == null)
                continue;

            int materialCount = 1;
            if (originalRendererMaterials.TryGetValue(renderer, out Material[] rendererOriginals) && rendererOriginals != null)
                materialCount = Mathf.Max(1, rendererOriginals.Length);

            Material[] markMats = new Material[materialCount];
            for (int i = 0; i < markMats.Length; i++)
                markMats[i] = material;

            renderer.sharedMaterials = markMats;
        }
    }

    private void RestoreOriginalMaterialsOnAllRenderers()
    {
        if (!HasCachedRenderers())
            return;

        foreach (KeyValuePair<Renderer, Material[]> pair in originalRendererMaterials)
        {
            if (pair.Key != null && pair.Value != null)
                pair.Key.sharedMaterials = pair.Value;
        }
    }

    private void ClearPropertyBlocksOnAllRenderers()
    {
        if (!HasCachedRenderers())
            return;

        foreach (Renderer renderer in enemyRenderers)
        {
            if (renderer == null)
                continue;

            renderer.GetPropertyBlock(propBlock);
            propBlock.Clear();
            renderer.SetPropertyBlock(propBlock);
        }
    }

    private IEnumerator MarkExpirationRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        ApplyMarkedStatus(1.0f);
    }

    public void ApplyBleed(float dps, float duration)
    {
        if (networkedEnemy != null && !networkedEnemy.IsServer) return;
        StartCoroutine(BleedRoutine(dps, duration));
    }
    private IEnumerator BleedRoutine(float dps, float duration)
    {
        float t = 0;
        while (t < duration)
        {
            yield return new WaitForSeconds(1f);
            if (isDead) break;
            TakeDamage(dps, 1f);
            t += 1f;
        }
    }

    private int revealCounter = 0;
    public void ApplyReveal(float duration)
    {
        if (networkedEnemy != null && !networkedEnemy.IsServer) return;
        StartCoroutine(RevealRoutine(duration));
    }
    private IEnumerator RevealRoutine(float duration)
    {
        revealCounter++;
        ApplyMaterialToAllRenderers(markedMaterial);

        yield return new WaitForSeconds(duration);
        revealCounter--;

        if (revealCounter <= 0 && !isMarked)
            RestoreOriginalMaterialsOnAllRenderers();
    }

    public bool IsArmorShredded => armorShredStacks > 0;

    public void AplicarVulnerabilidadeTemporaria(float multiplier, float duration)
    {
        vulnerabilityMultiplier = multiplier;
        if (vulnerabilityCoroutine != null) StopCoroutine(vulnerabilityCoroutine);
        vulnerabilityCoroutine = StartCoroutine(VulnerabilityRoutine(duration));
    }

    private IEnumerator VulnerabilityRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);
        vulnerabilityMultiplier = 1f;
    }

    public void AplicarVulnerabilidade(float multiplier) => vulnerabilityMultiplier = multiplier;
    public void RemoverVulnerabilidade() => vulnerabilityMultiplier = 1f;

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        if (networkedEnemy != null && networkedEnemy.IsServer)
            StartCoroutine(networkedEnemy.DieRoutine());

        if (CurrencyManager.Instance != null && enemyData != null && (networkedEnemy == null || networkedEnemy.IsServer))
        {
            CurrencyManager.Instance.AddCurrency(enemyData.geoditasOnDeath, CurrencyType.Geodites);
            if (Random.value <= enemyData.etherDropChance)
                CurrencyManager.Instance.AddCurrency(1, CurrencyType.DarkEther);
        }

        if (enemyController != null) enemyController.HandleDeath();

        if (networkedEnemy == null && deathVfxPrefab != null)
        {
            GlobalVFXPool.GetVFX(deathVfxPrefab, transform.position, transform.rotation, 4f);
            TriggerDeathDissolve();
        }
    }

    /// <summary>
    /// Inicia o efeito de dissolve de morte. Pode ser chamado via:
    /// 1) Codigo direto (como ja era feito)
    /// 2) UnityEvent no Inspector
    /// 3) Animation Event dentro de uma animacao de morte
    /// </summary>
    public void TriggerDeathDissolve()
    {
        Debug.Log($"[EnemyHealth] TriggerDeathDissolve() chamado em '{gameObject.name}'");
        OnDeathDissolveStart?.Invoke();
        StartCoroutine(DeathDissolveRoutine());
    }

    private IEnumerator DeathDissolveRoutine()
    {
        if (!HasCachedRenderers())
        {
            Debug.LogWarning($"[EnemyHealth] DeathDissolve ABORTADO em '{gameObject.name}': Nenhum Renderer encontrado!");
            yield break;
        }

        Debug.Log($"[EnemyHealth] DeathDissolve iniciando em '{gameObject.name}'. " +
                  $"Renderers: {enemyRenderers.Length}, Propriedade: '{dissolveAmountProperty}', Duracao: {dissolveDuration}s");

        // Cria instancias dos materiais para poder modificar sem afetar o asset original
        Dictionary<Renderer, Material[]> instanceMaterials = new Dictionary<Renderer, Material[]>();
        int totalMateriais = 0;
        int materiaisComPropriedade = 0;

        foreach (Renderer renderer in enemyRenderers)
        {
            if (renderer == null) continue;
            Material[] mats = renderer.materials;
            instanceMaterials[renderer] = mats;

            foreach (Material mat in mats)
            {
                totalMateriais++;
                bool temProp = mat != null && mat.HasProperty(dissolveAmountProperty);
                if (temProp) materiaisComPropriedade++;

                Debug.Log($"[EnemyHealth]   Renderer '{renderer.gameObject.name}' -> Material '{(mat != null ? mat.name : "NULL")}' " +
                          $"| Tem '{dissolveAmountProperty}': {temProp}" +
                          $" | Shader: {(mat != null ? mat.shader.name : "N/A")}");
            }
        }

        Debug.Log($"[EnemyHealth] Total de materiais: {totalMateriais}, Com propriedade '{dissolveAmountProperty}': {materiaisComPropriedade}");

        if (materiaisComPropriedade == 0)
        {
            Debug.LogError($"[EnemyHealth] NENHUM material em '{gameObject.name}' tem a propriedade '{dissolveAmountProperty}'! " +
                           "Verifique o nome no shader (ex: _DissolveAmount, _Dissolve, _Cutoff, _Alpha, etc.)");
        }

        float elapsedTime = 0f;

        while (elapsedTime < dissolveDuration)
        {
            elapsedTime += Time.deltaTime;
            float dissolveValue = Mathf.Clamp01(elapsedTime / dissolveDuration);

            foreach (var pair in instanceMaterials)
            {
                if (pair.Key == null) continue;
                foreach (Material mat in pair.Value)
                {
                    if (mat != null && mat.HasProperty(dissolveAmountProperty))
                        mat.SetFloat(dissolveAmountProperty, dissolveValue);
                }
            }
            yield return null;
        }

        // Garante que o valor final seja 1 (totalmente dissolvido)
        foreach (var pair in instanceMaterials)
        {
            if (pair.Key == null) continue;
            foreach (Material mat in pair.Value)
            {
                if (mat != null && mat.HasProperty(dissolveAmountProperty))
                    mat.SetFloat(dissolveAmountProperty, 1f);
            }
        }

        Debug.Log($"[EnemyHealth] DeathDissolve CONCLUIDO em '{gameObject.name}'");
    }
}
