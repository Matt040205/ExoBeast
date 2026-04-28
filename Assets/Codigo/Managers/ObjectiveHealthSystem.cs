using System;
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

        localHealth = maxHealth;
    }

    private void Start()
    {
        bool ngoActive = NetworkManager.Singleton != null &&
                         (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient);

        if (!ngoActive)
        {
            isNetworkDriven = false;
            localHealth = maxHealth;
            NotifyHealthChanged();
        }
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        isNetworkDriven = true;
        if (IsServer)
            currentHealth.Value = maxHealth;

        currentHealth.OnValueChanged += OnCurrentHealthChanged;
        NotifyHealthChanged();
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnCurrentHealthChanged;
        isNetworkDriven = false;
        base.OnNetworkDespawn();
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
            NotifyHealthChanged();

            if (localHealth <= 0f)
                Die();
        }
    }

    private void OnCurrentHealthChanged(float oldValue, float newValue)
    {
        NotifyHealthChanged();
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

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke();
        ObjectiveHealthBus.Publish(CurrentHealth, maxHealth);
    }
}
