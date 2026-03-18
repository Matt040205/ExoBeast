using UnityEngine;
using System;
using System.Collections;
using Unity.Netcode;

public class PlayerHealthSystem : NetworkBehaviour
{
    public CharacterBase characterData;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public bool isRegenerating;

    [Header("Status de Buffs")]
    public float damageMultiplier = 1f;
    public float speedMultiplier = 1f;
    public float damageResistance = 0f;
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
        if (IsServer)
        {
            currentHealth.Value = characterData.maxHealth;
        }

        currentHealth.OnValueChanged += (oldValue, newValue) => NotifyHealthChanged();
        NotifyHealthChanged();
        FindRespawnPoint();
    }

    void Update()
    {
        if (!IsServer) return;
        HandleRegeneration();
    }

    public void ApplyBuffs(float newDamageMult, float newSpeedMult, float duration)
    {
        if (buffCoroutine != null) StopCoroutine(buffCoroutine);

        damageMultiplier = newDamageMult;
        speedMultiplier = newSpeedMult;
        isBuffed = true;

        buffCoroutine = StartCoroutine(RemoveBuffsAfterTime(duration));
    }

    private IEnumerator RemoveBuffsAfterTime(float duration)
    {
        yield return new WaitForSeconds(duration);

        damageMultiplier = 1f;
        speedMultiplier = 1f;
        isBuffed = false;
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

        float finalDamage = damage * (1f - damageResistance);
        if (finalDamage < 0) finalDamage = 0;

        currentHealth.Value -= finalDamage;
        timeSinceLastDamage = 0f;
        isRegenerating = false;

        if (currentHealth.Value <= 0) Die();
    }

    public void Heal(float amount)
    {
        if (!IsServer) return;
        currentHealth.Value = Mathf.Min(currentHealth.Value + amount, characterData.maxHealth);
    }

    void Die()
    {
        if (respawnPoint == null) FindRespawnPoint();

        if (respawnPoint != null)
        {
            CharacterController controller = GetComponent<CharacterController>();
            MonoBehaviour movementScript = GetComponent("PlayerMovement") as MonoBehaviour;

            if (controller != null) controller.enabled = false;
            if (movementScript != null) movementScript.enabled = false;

            transform.position = respawnPoint.position;

            StartCoroutine(ReactivatePlayer(controller, movementScript));
        }

        currentHealth.Value = characterData.maxHealth;

        damageMultiplier = 1f;
        speedMultiplier = 1f;
        isCountering = false;
    }

    private IEnumerator ReactivatePlayer(CharacterController controller, MonoBehaviour movementScript)
    {
        yield return null;

        if (controller != null) controller.enabled = true;
        if (movementScript != null) movementScript.enabled = true;
    }

    void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke();
    }
}