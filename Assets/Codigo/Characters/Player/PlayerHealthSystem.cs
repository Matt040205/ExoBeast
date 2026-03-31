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
            currentHealth.Value += characterData.maxHealth * 0.01f * Time.deltaTime;
            currentHealth.Value = Mathf.Min(currentHealth.Value, characterData.maxHealth);
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
        if (finalDamage < 0) finalDamage = 0;

        currentHealth.Value -= finalDamage;
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
            currentHealth.Value = Mathf.Min(currentHealth.Value + amount, characterData.maxHealth);
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
            CharacterController controller = GetComponent<CharacterController>();
            MonoBehaviour movementScript = GetComponent("PlayerMovement") as MonoBehaviour;

            if (controller != null) controller.enabled = false;
            if (movementScript != null) movementScript.enabled = false;

            transform.position = spawnPosition;

            StartCoroutine(ReactivatePlayer(controller, movementScript));
        }

        // Efeito visual de respawn para todos
        PlayRespawnEffect();
    }

    private IEnumerator ReactivatePlayer(CharacterController controller, MonoBehaviour movementScript)
    {
        yield return null; // Esperar um frame para o teleporte ser processado pela engine de física
        if (controller != null) controller.enabled = true;
        if (movementScript != null) movementScript.enabled = true;
    }

    private void PlayRespawnEffect()
    {
        // TODO: Implementar ou chamar efeito visual/sonoro de respawn
    }

    void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke();
    }
}