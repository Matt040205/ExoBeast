using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using ExoBeasts.Multiplayer.Sync;

/// <summary>
/// ── EnemyHealthSystem ──────────────────────────────────
/// Sistema de vida do inimigo vinculado ao NetworkedEnemy.
///
///  ▸ Server: processa dano (armor, crit, mark, vulnerability), sincroniza NetworkHealth
///  ▸ Client: recebe ShowHitVisualLocal via ClientRpc para flash e popup de dano
///  ▸ Die(): inicia DieRoutine do NetworkedEnemy e entrega recompensas
///  ▸ Suporta armor shred, marked status e vulnerability stacks
/// ─────────────────────────────────────────────────────
/// </summary>
public class EnemyHealthSystem : MonoBehaviour
{
    [Header("Referências")]
    public EnemyDataSO enemyData;
    public Material markedMaterial;
    private Renderer enemyRenderer;
    private WorldSpaceEnemyUI worldSpaceUI; 

    private Material[] originalMaterials;

    [Header("Feedback Visual")]
    [Tooltip("Ponto de spawn dos números de dano")]
    public Transform popupSpawnPoint;

    [Header("Hit Flash (Juice)")]
    public Color flashColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    public float flashDuration = 0.05f;
    public string flashAmountProperty = "_FlashAmount";
    public string flashColorProperty = "_FlashColor";

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
        
        enemyRenderer = GetComponent<Renderer>();
        if (enemyRenderer == null)
            enemyRenderer = GetComponentInChildren<Renderer>();

        propBlock = new MaterialPropertyBlock();

        if (enemyRenderer != null)
        {
            originalMaterials = enemyRenderer.materials.ToArray();
        }
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

        if (enemyRenderer != null && originalMaterials != null)
        {
            enemyRenderer.materials = originalMaterials;
            enemyRenderer.GetPropertyBlock(propBlock);
            propBlock.Clear();
            enemyRenderer.SetPropertyBlock(propBlock);
        }
        isMarked = false;

        if (networkedEnemy != null && networkedEnemy.IsServer)
        {
            networkedEnemy.NetworkHealth.Value = currentHealth;
            networkedEnemy.IsDead.Value = false;
        }
    }

    public bool TakeDamage(float damage, float armorPenetration = 0f, bool isCritical = false)
    {
        // Só bloqueia se NGO está ativo E não somos o servidor (somos cliente puro)
        bool isNGOSpawned = networkedEnemy != null && networkedEnemy.IsSpawned;
        if (isNGOSpawned && !networkedEnemy.IsServer) return false;
        if (isDead) return false;

        float damageWithMark = damage * markedDamageMultiplier * vulnerabilityMultiplier;
        float armorToIgnore = baseArmor * armorPenetration;
        float effectiveArmor = Mathf.Max(0, baseArmor - currentArmorModifier - armorToIgnore);
        float finalDamage = damageWithMark * (1.0f - Mathf.Clamp01(effectiveArmor / 100f));
        if (finalDamage < 0) finalDamage = 0;

        currentHealth -= finalDamage;

        if (networkedEnemy != null)
        {
            networkedEnemy.NetworkHealth.Value = currentHealth;
            if (finalDamage > 0)
                networkedEnemy.TriggerHitVisual(finalDamage, isCritical);
        }

        if (currentHealth <= 0)
        {
            Die();
            return true;
        }
        return false;
    }

    public void ShowHitVisualLocal(float damageAmount, bool isCritical)
    {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
        flashCoroutine = StartCoroutine(HitFlashRoutine());

        SpawnDamagePopupLocal((int)damageAmount, isCritical);
    }

    private IEnumerator HitFlashRoutine()
    {
        if (enemyRenderer != null)
        {
            enemyRenderer.GetPropertyBlock(propBlock);
            propBlock.SetFloat(flashAmountProperty, 1f);
            propBlock.SetColor(flashColorProperty, flashColor);
            enemyRenderer.SetPropertyBlock(propBlock);

            yield return new WaitForSeconds(flashDuration);

            if (enemyRenderer != null)
            {
                enemyRenderer.GetPropertyBlock(propBlock);
                propBlock.SetFloat(flashAmountProperty, 0f);
                enemyRenderer.SetPropertyBlock(propBlock);
            }
        }
    }

    private void SpawnDamagePopupLocal(int damageAmount, bool isCritical)
    {
        Vector3 spawnPos = popupSpawnPoint != null ? popupSpawnPoint.position : transform.position + Vector3.up * 1.5f;
        
        if (UIPoolManager.Instance != null)
            UIPoolManager.Instance.SpawnDamagePopup(spawnPos, damageAmount, isCritical);
        else if (worldSpaceUI != null)
            worldSpaceUI.ShowDamageNumber((float)damageAmount, isCritical);
    }

    public void ApplyArmorShred(float percentage, int maxStacks)
    {
        bool isNGOSpawned = networkedEnemy != null && networkedEnemy.IsSpawned;
        if (isNGOSpawned && !networkedEnemy.IsServer) return;
        if (armorShredStacks < maxStacks)
        {
            armorShredStacks++;
            currentArmorModifier += percentage;
        }
    }

    public void ApplyMarkedStatus(float multiplier)
    {
        bool isNGOSpawned = networkedEnemy != null && networkedEnemy.IsSpawned;
        if (isNGOSpawned && !networkedEnemy.IsServer) return;
        
        markedDamageMultiplier = multiplier;

        // Feedback visual da marcação: Substitui materiais temporariamente
        if (multiplier > 1.0f && enemyRenderer != null && markedMaterial != null)
        {
            // Força o material a renderizar através de paredes independentemente do Shader URP
            markedMaterial.SetInt("_ZTest", (int)UnityEngine.Rendering.CompareFunction.Always);

            Material[] markMats = new Material[originalMaterials.Length];
            for (int i = 0; i < markMats.Length; i++)
            {
                markMats[i] = markedMaterial;
            }
            enemyRenderer.materials = markMats;
            isMarked = true;
        }
        else if (multiplier <= 1.0f && enemyRenderer != null && originalMaterials != null && isMarked)
        {
            // Retorna aos materiais originais e reseta o Property Block
            enemyRenderer.materials = originalMaterials;
            enemyRenderer.SetPropertyBlock(propBlock);
            isMarked = false;
        }
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

    public void AplicarVulnerabilidade(float multiplier)
    {
        vulnerabilityMultiplier = multiplier;
    }

    public void RemoverVulnerabilidade()
    {
        vulnerabilityMultiplier = 1f;
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        bool isNGOSpawned = networkedEnemy != null && networkedEnemy.IsSpawned;
        bool isLocalMode = !isNGOSpawned;

        if (isNGOSpawned && networkedEnemy.IsServer)
        {
            // Modo rede: Iniciar sequência de morte sincronizada
            StartCoroutine(networkedEnemy.DieRoutine());
        }

        // Recompensas entregues em modo local OU pelo servidor em modo rede
        if (isLocalMode || (isNGOSpawned && networkedEnemy.IsServer))
        {
            if (CurrencyManager.Instance != null && enemyData != null)
            {
                CurrencyManager.Instance.AddCurrency(enemyData.geoditasOnDeath, CurrencyType.Geodites);
                if (Random.value <= enemyData.etherDropChance)
                    CurrencyManager.Instance.AddCurrency(1, CurrencyType.DarkEther);
            }
        }

        if (enemyController != null)
            enemyController.HandleDeath();
        else if (networkedEnemy == null)
            gameObject.SetActive(false);
    }
}
