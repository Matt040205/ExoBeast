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
        StartCoroutine(SpawnMaterializationFlow());
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

        StartCoroutine(SpawnMaterializationFlow());
    }

    private IEnumerator SpawnMaterializationFlow()
    {
        // Passo A: Bloqueio
        CharacterController controller = null;
        MonoBehaviour movementScript = null;
        MonoBehaviour shootingScript = null;
        MonoBehaviour combatScript = null;

        if (IsOwner)
        {
            controller = GetComponent<CharacterController>();
            movementScript = GetComponent("PlayerMovement") as MonoBehaviour;
            shootingScript = GetComponent("PlayerShooting") as MonoBehaviour;
            combatScript = GetComponent("PlayerCombatManager") as MonoBehaviour;

            if (controller != null) controller.enabled = false;
            if (movementScript != null) movementScript.enabled = false;
            if (shootingScript != null) shootingScript.enabled = false;
            if (combatScript != null) combatScript.enabled = false;
        }

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
            Material[] finalMaterials = new Material[] { materialToon, materialOutline };
            foreach (Renderer r in targetRenderers)
            {
                r.materials = finalMaterials;
            }
        }

        // Passo F: Liberação
        if (IsOwner)
        {
            if (controller != null) controller.enabled = true;
            if (movementScript != null) movementScript.enabled = true;
            if (shootingScript != null) shootingScript.enabled = true;
            if (combatScript != null) combatScript.enabled = true;
        }
    }

    void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke();
    }
}