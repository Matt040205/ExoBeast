using UnityEngine;
using System;
using System.Collections;
using Unity.Netcode;

/// <summary>
/// ── PlayerHealthSystem ─────────────────────────────────
/// Sistema de vida do jogador com autoridade no servidor.
///
///  ▸ NetworkVariables: currentHealth, damageMultiplier, speedMultiplier, damageResistance
///  ▸ Server: aplica dano, cura, buffs, regeneracao e respawn
///  ▸ Client: recebe RespawnClientRpc para teleporte e efeito visual
///  ▸ Suporta counter (dano refletido) e buffs temporarios
/// ─────────────────────────────────────────────────────
/// </summary>
public class PlayerHealthSystem : NetworkBehaviour
{
    public CharacterBase characterData;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    
    [Header("Networked Buffs")]
    public NetworkVariable<float> damageMultiplier = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> speedMultiplier = new NetworkVariable<float>(1f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> damageResistance = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public bool isRegenerating;
    public bool isBuffed = false;

    [Header("Status de Defesa")]
    public bool isCountering = false;

    private float timeSinceLastDamage;
    private Transform respawnPoint;
    private Coroutine buffCoroutine;
    private Coroutine spawnMaterializationCoroutine;

    [Header("Configuração de Respawn")]
    public string respawnPointNameOrTag = "RespawnPoint";

    [Header("Materialização (Spawn)")]
    [SerializeField] private float tempoDeSpawn = 2f;
    [SerializeField] private Material materialHolograma;
    [SerializeField] private Material materialToon;
    [SerializeField] private Material materialOutline;


    public event Action OnHealthChanged;
    public event Action<float> OnDamageDealt;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            if (characterData != null)
                currentHealth.Value = characterData.maxHealth;
            else
                Debug.LogWarning("[PlayerHealthSystem] characterData não atribuído!");
        }

        currentHealth.OnValueChanged += (oldValue, newValue) => NotifyHealthChanged();
        
        // Inicializar UI local
        NotifyHealthChanged();
        
        FindRespawnPoint();

        // Registrar no HUD se for o dono
        if (IsOwner)
        {
            StartCoroutine(WaitAndRegisterHUD());
        }

        // Inicia o fluxo de materialização ao spawnar pela primeira vez
        RestartSpawnMaterializationFlow();
    }

    private IEnumerator WaitAndRegisterHUD()
    {
        Debug.Log("[PlayerHealthSystem] Aguardando PlayerHUD ligar na cena...");
        yield return new WaitUntil(() => PlayerHUD.Instance != null);
        Debug.Log("[PlayerHealthSystem] PlayerHUD encontrado! Registrando referências de Vida e Munição...");
        PlayerHUD.Instance.RegistrarJogador(this);
    }

    void Update()
    {
        if (!IsServer) return;
        HandleRegeneration();
    }

    public void ApplyBuffs(float newDamageMult, float newSpeedMult, float duration)
    {
        if (!IsServer) return;

        if (buffCoroutine != null) StopCoroutine(buffCoroutine);

        damageMultiplier.Value = newDamageMult;
        speedMultiplier.Value = newSpeedMult;
        isBuffed = true;

        buffCoroutine = StartCoroutine(RemoveBuffsAfterTime(duration));
    }

    private IEnumerator RemoveBuffsAfterTime(float duration)
    {
        yield return new WaitForSeconds(duration);

        if (IsServer)
        {
            damageMultiplier.Value = 1f;
            speedMultiplier.Value = 1f;
            isBuffed = false;
        }
        buffCoroutine = null;
    }

    public void TriggerDamageDealt(float damageAmount)
    {
        OnDamageDealt?.Invoke(damageAmount);
    }

    void FindRespawnPoint()
    {
        GameObject respawnObject = GameObject.FindWithTag(respawnPointNameOrTag);
        if (respawnObject == null) respawnObject = GameObject.Find(respawnPointNameOrTag);

        if (respawnObject != null)
        {
            respawnPoint = respawnObject.transform;
        }
    }

    void HandleRegeneration()
    {
        if (characterData == null) return;

        if (currentHealth.Value >= characterData.maxHealth)
        {
            isRegenerating = false;
            return;
        }

        timeSinceLastDamage += Time.deltaTime;

        if (timeSinceLastDamage >= 3f)
        {
            isRegenerating = true;
            float previousHealth = currentHealth.Value;
            
            currentHealth.Value += characterData.maxHealth * 0.01f * Time.deltaTime;
            currentHealth.Value = Mathf.Min(currentHealth.Value, characterData.maxHealth);

            // VFX de cura passiva é acionado automaticamente pelo HealVFXReactor
            // via currentHealth.OnValueChanged, que propaga para todos os clientes.
        }
    }

    public void TakeDamage(float damage, Transform attacker = null)
    {
        if (!IsServer) return;

        if (isCountering)
        {
            if (attacker != null)
            {
                EnemyHealthSystem enemyHealth = attacker.GetComponent<EnemyHealthSystem>();
                if (enemyHealth != null)
                {
                    enemyHealth.TakeDamage(damage);
                }

                EnemyController enemyController = attacker.GetComponent<EnemyController>();
                if (enemyController != null)
                {
                    enemyController.ApplySlip();
                }
            }
            return;
        }

        float finalDamage = damage * (1f - damageResistance.Value);

        AllyShield shield = GetComponent<AllyShield>();
        if (shield != null && shield.IsActive)
        {
            finalDamage = shield.AbsorbDamage(finalDamage);
        }

        DragonAuraBuff auraBuff = GetComponent<DragonAuraBuff>();
        if (auraBuff != null && auraBuff.DamageReduction > 0)
        {
            finalDamage *= (1f - auraBuff.DamageReduction);
        }

        if (finalDamage < 0) finalDamage = 0;

        currentHealth.Value -= finalDamage;
        // Zera o tempo para não curar o efeito visual de uma vez e interrompe regen normal
        timeSinceLastDamage = 0f;
        isRegenerating = false;

        // Visual de hit ClientRpc aqui se necessário
        // TakeDamageVisualClientRpc();

        if (currentHealth.Value <= 0) Die();
    }

    public void Heal(float amount)
    {
        if (!IsServer) return;
        if (characterData != null)
        {
            float previousHealth = currentHealth.Value;
            currentHealth.Value = Mathf.Min(currentHealth.Value + amount, characterData.maxHealth);

            // VFX de cura é acionado automaticamente pelo HealVFXReactor
            // via currentHealth.OnValueChanged, que propaga para todos os clientes.
        }
    }


    void Die()
    {
        if (!IsServer) return;

        if (respawnPoint == null) FindRespawnPoint();

        Vector3 spawnPos = Vector3.zero;
        if (respawnPoint != null)
        {
            spawnPos = respawnPoint.position;
        }

        // Resetar status no servidor
        currentHealth.Value = (characterData != null) ? characterData.maxHealth : 100f;
        damageMultiplier.Value = 1f;
        speedMultiplier.Value = 1f;
        isCountering = false;

        // Chamar respawn em todos os clientes, especialmente no dono para teleporte
        RespawnClientRpc(spawnPos);
    }

    [ClientRpc]
    private void RespawnClientRpc(Vector3 spawnPosition)
    {
        if (IsOwner)
        {
            transform.position = spawnPosition;
            Physics.SyncTransforms(); // Sincroniza a física imediatamente para o teleporte
        }

        RestartSpawnMaterializationFlow();
    }

    private void RestartSpawnMaterializationFlow()
    {
        if (spawnMaterializationCoroutine != null)
            StopCoroutine(spawnMaterializationCoroutine);

        spawnMaterializationCoroutine = StartCoroutine(SpawnMaterializationFlow());
    }

    private void RestoreOwnerGameplayState(
        CharacterController controller,
        bool controllerWasEnabled,
        PlayerMovement movementScript,
        bool movementWasEnabled,
        PlayerShooting shootingScript,
        bool shootingWasEnabled,
        PlayerCombatManager combatScript,
        bool combatWasEnabled)
    {
        if (!IsOwner)
            return;

        if (controller != null)
            controller.enabled = controllerWasEnabled;

        if (movementScript != null)
            movementScript.enabled = movementWasEnabled;

        if (shootingScript != null)
            shootingScript.enabled = shootingWasEnabled;

        if (combatScript != null)
            combatScript.enabled = combatWasEnabled;
    }

    private IEnumerator SpawnMaterializationFlow()
    {
        // Passo A: Bloqueio
        CharacterController controller = null;
        PlayerMovement movementScript = null;
        PlayerShooting shootingScript = null;
        PlayerCombatManager combatScript = null;
        bool controllerWasEnabled = false;
        bool movementWasEnabled = false;
        bool shootingWasEnabled = false;
        bool combatWasEnabled = false;

        if (IsOwner)
        {
            controller = GetComponent<CharacterController>();
            movementScript = GetComponent<PlayerMovement>();
            shootingScript = GetComponent<PlayerShooting>();
            combatScript = GetComponent<PlayerCombatManager>();

            controllerWasEnabled = controller != null && controller.enabled;
            movementWasEnabled = movementScript != null && movementScript.enabled;
            shootingWasEnabled = shootingScript != null && shootingScript.enabled;
            combatWasEnabled = combatScript != null && combatScript.enabled;

            if (controllerWasEnabled) controller.enabled = false;
            if (movementWasEnabled) movementScript.enabled = false;
            if (shootingWasEnabled) shootingScript.enabled = false;
            if (combatWasEnabled) combatScript.enabled = false;
        }

        try
        {
        Renderer[] allRenderers = GetComponentsInChildren<Renderer>(true);
        System.Collections.Generic.List<Renderer> targetRenderers = new System.Collections.Generic.List<Renderer>();
        
        // Dicionário para guardar as texturas originais
        System.Collections.Generic.Dictionary<Renderer, Texture> texturasOriginais = new System.Collections.Generic.Dictionary<Renderer, Texture>();

        foreach (Renderer r in allRenderers)
        {
            if (r is MeshRenderer || r is SkinnedMeshRenderer)
            {
                targetRenderers.Add(r);

                // Tenta extrair a textura original antes de trocar pelo holograma
                if (r.sharedMaterial != null)
                {
                    if (r.sharedMaterial.HasProperty("_BaseMap"))
                        texturasOriginais[r] = r.sharedMaterial.GetTexture("_BaseMap");
                    else if (r.sharedMaterial.HasProperty("_MainTex"))
                        texturasOriginais[r] = r.sharedMaterial.GetTexture("_MainTex");
                }
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

        // Passo C e D: Animação do Shader pelo tempoDeSpawn
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
            foreach (Renderer r in targetRenderers)
            {
                // Cria uma instância do material toon para não alterar o original
                Material matToonInstance = new Material(materialToon);

                // Restaura a textura original salva
                if (texturasOriginais.TryGetValue(r, out Texture texOriginal) && texOriginal != null)
                {
                    if (matToonInstance.HasProperty("_BaseMap"))
                        matToonInstance.SetTexture("_BaseMap", texOriginal);
                    else if (matToonInstance.HasProperty("_MainTex"))
                        matToonInstance.SetTexture("_MainTex", texOriginal);
                }

                Material[] finalMaterials = new Material[] { matToonInstance, materialOutline };
                r.materials = finalMaterials;
            }
        }

        // Passo F: Liberação
        }
        finally
        {
            RestoreOwnerGameplayState(
                controller,
                controllerWasEnabled,
                movementScript,
                movementWasEnabled,
                shootingScript,
                shootingWasEnabled,
                combatScript,
                combatWasEnabled);

            spawnMaterializationCoroutine = null;
        }
    }

    void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke();
    }
}
