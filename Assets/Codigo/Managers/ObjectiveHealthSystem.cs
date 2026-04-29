using System;
using System.Collections;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

public class ObjectiveHealthSystem : NetworkBehaviour
{
    public static ObjectiveHealthSystem Instance { get; private set; }

    [Header("Configuracoes de Vida (Sincronizada)")]
    public float maxHealth = 100f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public event Action OnHealthChanged;

    private bool isDead;
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
            localHealth = Mathf.Max(maxHealth, 1f);
            PublishHealthSnapshot();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        isDead = false;
        isNetworkDriven = true;
        if (IsServer)
            currentHealth.Value = maxHealth;

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
                Die();
        }
        else
        {
            localHealth = Mathf.Max(localHealth - damage, 0f);
            PublishHealthSnapshot();

            if (localHealth <= 0f)
                Die();
        }
    }

    private void OnCurrentHealthChanged(float oldValue, float newValue)
    {
        localHealth = Mathf.Clamp(newValue, 0f, maxHealth);
        PublishHealthSnapshot();
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
