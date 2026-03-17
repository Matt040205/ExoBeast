using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class EnemyHealthSystem : MonoBehaviour
{
    [Header("Referências")]
    public EnemyDataSO enemyData;
    public Material markedMaterial;
    private Renderer enemyRenderer;
    private WorldSpaceEnemyUI worldSpaceUI; // Fallback antigo

    private Material[] originalMaterials;

    [Header("Feedback Visual (Novo)")]
    [Tooltip("Opcional: Ponto exato onde o texto vai nascer (ex: um Empty na cabeça do inimigo)")]
    public Transform popupSpawnPoint;

    [Header("Hit Flash (Juice)")]
    [Tooltip("Cor do flash (Branco acinzentado para não machucar os olhos)")]
    public Color flashColor = new Color(0.8f, 0.8f, 0.8f, 1f);
    [Tooltip("Duração do piscar em segundos (bem rapidinho)")]
    public float flashDuration = 0.05f;
    [Tooltip("Nome da propriedade Float no Shader Graph")]
    public string flashAmountProperty = "_FlashAmount";
    [Tooltip("Nome da propriedade Color no Shader Graph")]
    public string flashColorProperty = "_FlashColor";

    private Coroutine flashCoroutine;
    private MaterialPropertyBlock propBlock;

    [Header("Status Atual")]
    public float currentHealth;
    public bool isDead;

    private float baseArmor;
    private float currentArmorModifier = 0f;
    private int armorShredStacks = 0;
    private float markedDamageMultiplier = 1f;
    private float vulnerabilityMultiplier = 1f;

    private EnemyController enemyController;
    private bool isMarked = false;
    private Coroutine vulnerabilityCoroutine;

    public bool IsArmorShredded => armorShredStacks > 0;

    void Awake()
    {
        enemyController = GetComponent<EnemyController>();
        worldSpaceUI = GetComponentInChildren<WorldSpaceEnemyUI>();
        enemyRenderer = GetComponent<Renderer>();

        // Inicializa o bloco de propriedades para o Hit Flash
        propBlock = new MaterialPropertyBlock();

        if (enemyRenderer == null)
        {
            enemyRenderer = GetComponentInChildren<Renderer>();
        }

        if (enemyRenderer != null)
        {
            originalMaterials = enemyRenderer.materials.ToArray();
        }
        else
        {
            Debug.LogError($"<color=red>EnemyHealthSystem:</color> NENHUM RENDERER ENCONTRADO para o inimigo '{gameObject.name}'. A marcação visual NÃO FUNCIONARÁ!");
        }
    }

    public void InitializeHealth(int level)
    {
        if (enemyData == null)
        {
            Debug.LogError("EnemyData não atribuído em " + gameObject.name);
            return;
        }
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
            // Limpa qualquer override que tenha ficado
            enemyRenderer.GetPropertyBlock(propBlock);
            propBlock.Clear();
            enemyRenderer.SetPropertyBlock(propBlock);
        }
        isMarked = false;
    }

    public bool TakeDamage(float damage, float armorPenetration = 0f, bool isCritical = false)
    {
        if (isDead) return false;

        float damageWithMark = damage * markedDamageMultiplier * vulnerabilityMultiplier;

        float armorToIgnore = baseArmor * armorPenetration;
        float effectiveArmor = Mathf.Max(0, baseArmor - currentArmorModifier - armorToIgnore);
        float damageReduction = effectiveArmor;
        float damageMultiplier = 1f - damageReduction;

        float finalDamage = damageWithMark * damageMultiplier;

        if (markedDamageMultiplier > 1f || vulnerabilityMultiplier > 1f)
        {
            Debug.Log($"<color=orange>Dano Modificado:</color> Dano Base {damage.ToString("F1")}. Dano Final: {finalDamage.ToString("F1")}");
        }

        if (finalDamage > 0)
        {
            if (UIPoolManager.Instance != null)
            {
                SpawnDamagePopupLocal((int)finalDamage, isCritical);
            }
            else if (worldSpaceUI != null)
            {
                worldSpaceUI.ShowDamageNumber(finalDamage, isCritical);
            }

            // --- ACIONA O HIT FLASH AQUI ---
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(HitFlashRoutine());
        }

        currentHealth -= finalDamage;

        if (currentHealth <= 0)
        {
            Die();
            return true;
        }
        return false;
    }

    private IEnumerator HitFlashRoutine()
    {
        if (enemyRenderer != null)
        {
            enemyRenderer.GetPropertyBlock(propBlock);
            // Ativa o flash (1) e define a cor
            propBlock.SetFloat(flashAmountProperty, 1f);
            propBlock.SetColor(flashColorProperty, flashColor);
            enemyRenderer.SetPropertyBlock(propBlock);

            yield return new WaitForSeconds(flashDuration);

            if (enemyRenderer != null)
            {
                enemyRenderer.GetPropertyBlock(propBlock);
                // Desativa o flash voltando para (0)
                propBlock.SetFloat(flashAmountProperty, 0f);
                enemyRenderer.SetPropertyBlock(propBlock);
            }
        }
    }

    private void SpawnDamagePopupLocal(int damageAmount, bool isCritical)
    {
        Vector3 spawnPos = popupSpawnPoint != null ? popupSpawnPoint.position : transform.position + Vector3.up * 1.5f;
        UIPoolManager.Instance.SpawnDamagePopup(spawnPos, damageAmount, isCritical);
    }

    public void ApplyArmorShred(float percentage, int maxStacks)
    {
        if (armorShredStacks < maxStacks)
        {
            armorShredStacks++;
            currentArmorModifier += percentage;
            Debug.Log($"<color=purple>ARMOR SHRED:</color> {gameObject.name} shredado.");
        }
    }

    public void AplicarVulnerabilidade(float multiplicador)
    {
        vulnerabilityMultiplier = multiplicador;
    }

    public void RemoverVulnerabilidade()
    {
        vulnerabilityMultiplier = 1f;
    }

    public void AplicarVulnerabilidadeTemporaria(float multiplicador, float duracao)
    {
        if (vulnerabilityCoroutine != null) StopCoroutine(vulnerabilityCoroutine);

        vulnerabilityMultiplier = multiplicador;
        vulnerabilityCoroutine = StartCoroutine(ResetVulnerabilidadeRoutine(duracao));
    }

    private IEnumerator ResetVulnerabilidadeRoutine(float tempo)
    {
        yield return new WaitForSeconds(tempo);
        vulnerabilityMultiplier = 1f;
        vulnerabilityCoroutine = null;
    }

    public void ApplyMarkedStatus(float multiplier)
    {
        markedDamageMultiplier = multiplier;

        if (enemyRenderer != null && markedMaterial != null && !isMarked)
        {
            Material[] markedMaterialsArray = new Material[enemyRenderer.materials.Length];
            for (int i = 0; i < markedMaterialsArray.Length; i++)
            {
                markedMaterialsArray[i] = markedMaterial;
            }
            enemyRenderer.materials = markedMaterialsArray;
            isMarked = true;
        }
    }

    public void RemoveMarkedStatus()
    {
        markedDamageMultiplier = 1f;

        if (enemyRenderer != null && originalMaterials != null && isMarked)
        {
            enemyRenderer.materials = originalMaterials;
            isMarked = false;
        }
    }

    private void Die()
    {
        isDead = true;
        if (CurrencyManager.Instance != null && enemyData != null)
        {
            int geoditesAmount = enemyData.geoditasOnDeath;
            if (geoditesAmount > 0)
            {
                CurrencyManager.Instance.AddCurrency(geoditesAmount, CurrencyType.Geodites);
            }
            if (Random.value <= enemyData.etherDropChance)
            {
                CurrencyManager.Instance.AddCurrency(1, CurrencyType.DarkEther);
            }
        }
        if (enemyController != null)
        {
            enemyController.HandleDeath();
        }
        else
        {
            gameObject.SetActive(false);
        }
    }
}