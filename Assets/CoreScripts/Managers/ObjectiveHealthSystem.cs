using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ObjectiveHealthSystem : NetworkBehaviour
{
    public static ObjectiveHealthSystem Instance { get; private set; }

    [Header("Status da Base")]
    public float maxHealth = 20f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public event Action OnHealthChanged;

    [Header("FMOD - Sons")]
    public string somBaseAtacada = AudioEventIds.BaseHitLight;

    private bool isDead;
    private bool secondChanceUsed;
    private float localHealth;
    private bool isNetworkDriven;
    private Coroutine initialSyncRepublishCoroutine;

    public float CurrentHealth => isNetworkDriven ? currentHealth.Value : localHealth;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else if (Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        localHealth = Mathf.Max(maxHealth, 1f);
    }

    private void Start()
    {
        bool ngoActive = NetworkManager.Singleton != null &&
                         (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient);

        if (!ngoActive)
        {
            isNetworkDriven = false;
            localHealth = GetInitialHealth();
            PublishHealthSnapshot();
        }
        else if (NetworkObject != null && NetworkObject.IsSpawned)
        {
            // Adicionado via AddComponent em runtime após o NetworkObject ja ter spawnado.
            // NGO nao chama OnNetworkSpawn() nesse caso — inicializamos manualmente.
            InitializeNetworkState();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        InitializeNetworkState();
    }

    private void InitializeNetworkState()
    {
        if (isNetworkDriven)
            return;

        isDead = false;
        secondChanceUsed = false;
        isNetworkDriven = true;
        if (IsServer)
            currentHealth.Value = GetInitialHealth();

        localHealth = Mathf.Clamp(currentHealth.Value, 0f, maxHealth);
        currentHealth.OnValueChanged += OnCurrentHealthChanged;
        PublishHealthSnapshot();
        RestartInitialSyncRepublish();
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnCurrentHealthChanged;
        StopInitialSyncRepublish();
        localHealth = Mathf.Clamp(CurrentHealth, 0f, maxHealth);
        isNetworkDriven = false;
        base.OnNetworkDespawn();
    }

    public override void OnDestroy()
    {
        StopInitialSyncRepublish();

        if (Instance == this)
            Instance = null;

        base.OnDestroy();
    }

    public void TakeDamage(float damage)
    {
        if (isDead)
            return;

        if (isNetworkDriven)
        {
            if (!IsServer)
                return;

            currentHealth.Value = Mathf.Max(currentHealth.Value - damage, 0f);

            if (currentHealth.Value <= 0f)
            {
                if (TryConsumeSecondChance())
                    return;

                Die();
            }
        }
        else
        {
            float oldHealth = localHealth;
            localHealth = Mathf.Max(localHealth - damage, 0f);
            PublishHealthSnapshot();

            if (localHealth < oldHealth)
            {
                PlayBaseAttackedSound();
            }

            if (localHealth <= 0f)
            {
                if (TryConsumeSecondChance())
                    return;

                Die();
            }
        }
    }

    public void Heal(float amount)
    {
        if (amount <= 0f || isDead)
            return;

        if (isNetworkDriven)
        {
            if (!IsServer)
                return;

            currentHealth.Value = Mathf.Min(currentHealth.Value + amount, maxHealth);
        }
        else
        {
            localHealth = Mathf.Min(localHealth + amount, maxHealth);
            PublishHealthSnapshot();
        }
    }

    public void HealPercent(float percent)
    {
        Heal(maxHealth * Mathf.Max(0f, percent));
    }

    private float GetInitialHealth()
    {
        float multiplier = ModificacaoRunState.IsActive(ModificacaoGameplayEffect.NucleoFragil)
            ? ModificacaoRunState.GetValue(ModificacaoGameplayEffect.NucleoFragil, 0.9f)
            : 1f;

        return Mathf.Clamp(maxHealth * multiplier, 1f, maxHealth);
    }

    private bool TryConsumeSecondChance()
    {
        if (secondChanceUsed || !ModificacaoRunState.IsActive(ModificacaoGameplayEffect.SegundaChance))
            return false;

        secondChanceUsed = true;
        float restorePercent = ModificacaoRunState.GetValue(ModificacaoGameplayEffect.SegundaChance, 0.5f);

        if (isNetworkDriven)
            currentHealth.Value = Mathf.Clamp(maxHealth * restorePercent, 1f, maxHealth);
        else
            localHealth = Mathf.Clamp(maxHealth * restorePercent, 1f, maxHealth);

        EnemyHealthSystem[] enemies = FindObjectsByType<EnemyHealthSystem>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (EnemyHealthSystem enemy in enemies)
        {
            if (enemy != null && !enemy.isDead)
                enemy.ApplyAuthoritativeDamage(float.MaxValue, 0f, false, 0);
        }

        PublishHealthSnapshot();
        return true;
    }

    private void OnCurrentHealthChanged(float oldValue, float newValue)
    {
        localHealth = Mathf.Clamp(newValue, 0f, maxHealth);
        PublishHealthSnapshot();

        if (newValue < oldValue)
        {
            PlayBaseAttackedSound();
        }
    }

    private void PlayBaseAttackedSound()
    {
        if (!string.IsNullOrEmpty(somBaseAtacada))
        {
            ExoAudioService.PlayOneShot3D(somBaseAtacada, transform.position);
        }
    }

    private void RestartInitialSyncRepublish()
    {
        StopInitialSyncRepublish();
        initialSyncRepublishCoroutine = StartCoroutine(RepublishAfterInitialSync());
    }

    private void StopInitialSyncRepublish()
    {
        if (initialSyncRepublishCoroutine == null)
            return;

        StopCoroutine(initialSyncRepublishCoroutine);
        initialSyncRepublishCoroutine = null;
    }

    private IEnumerator RepublishAfterInitialSync()
    {
        for (int i = 0; i < 3; i++)
        {
            yield return null;
            localHealth = Mathf.Clamp(currentHealth.Value, 0f, maxHealth);
            PublishHealthSnapshot();
        }

        initialSyncRepublishCoroutine = null;
    }

    private void Die()
    {
        if (isDead)
            return;

        isDead = true;

        if (isNetworkDriven && IsServer)
            NetworkManager.Singleton.SceneManager.LoadScene("Lose", LoadSceneMode.Single);
        else
            SceneManager.LoadScene("Lose");
    }

    private void PublishHealthSnapshot()
    {
        float publishedHealth = isNetworkDriven
            ? Mathf.Clamp(currentHealth.Value, 0f, maxHealth)
            : Mathf.Clamp(localHealth, 0f, maxHealth);

        localHealth = publishedHealth;
        OnHealthChanged?.Invoke();
        ObjectiveHealthBus.Publish(publishedHealth, maxHealth);
    }
}
