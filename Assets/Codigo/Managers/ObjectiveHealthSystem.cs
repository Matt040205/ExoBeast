using UnityEngine;
using System;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// ── ObjectiveHealthSystem ──────────────────────────────
/// Vida do objetivo (cristal/base) com autoridade no servidor.
///
///  ▸ NetworkVariable currentHealth: sincroniza vida para todos (modo rede)
///  ▸ localHealth: fallback para modo local/singleplayer
///  ▸ Server/Local: TakeDamage, Die → carrega cena Lose
///  ▸ Client: OnHealthChanged atualiza UI local
/// ─────────────────────────────────────────────────────
/// </summary>
public class ObjectiveHealthSystem : NetworkBehaviour
{
    public static ObjectiveHealthSystem Instance { get; private set; }

    [Header("Configuracoes de Vida (Sincronizada)")]
    public float maxHealth = 100f;
    public NetworkVariable<float> currentHealth = new NetworkVariable<float>(100f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    public event Action OnHealthChanged;
    private bool isDead = false;

    // Fallback local para quando NGO não está ativo
    private float localHealth;
    private bool isNGOSpawned = false;

    /// <summary>Vida atual (funciona em local e rede)</summary>
    public float CurrentHealth => isNGOSpawned ? currentHealth.Value : localHealth;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        localHealth = maxHealth;
    }

    void Start()
    {
        // Se NGO não estiver ativo, inicializa em modo local
        bool ngoActive = NetworkManager.Singleton != null &&
                         (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient);
        if (!ngoActive)
        {
            isNGOSpawned = false;
            localHealth = maxHealth;
            Debug.Log($"[ObjectiveHealthSystem] Modo LOCAL. Vida: {localHealth}");
        }
    }

    private void OnCurrentHealthChanged(float oldVal, float newVal) => OnHealthChanged?.Invoke();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isNGOSpawned = true;

        if (IsServer)
        {
            currentHealth.Value = maxHealth;
        }

        currentHealth.OnValueChanged += OnCurrentHealthChanged;
        NotifyHealthChanged();
    }

    public override void OnNetworkDespawn()
    {
        currentHealth.OnValueChanged -= OnCurrentHealthChanged;
        isNGOSpawned = false;
        base.OnNetworkDespawn();
    }

    public void TakeDamage(float damage)
    {
        if (isDead) return;

        if (isNGOSpawned)
        {
            // Modo rede: só o servidor processa dano
            if (!IsServer) return;

            currentHealth.Value = Mathf.Max(currentHealth.Value - damage, 0);
            NotifyHealthChanged();

            if (currentHealth.Value <= 0)
                Die();
        }
        else
        {
            // Modo local: processa dano direto
            localHealth = Mathf.Max(localHealth - damage, 0);
            Debug.Log($"[ObjectiveHealthSystem] Dano local: -{damage}. Vida restante: {localHealth}");
            NotifyHealthChanged();

            if (localHealth <= 0)
                Die();
        }
    }

    private void Die()
    {
        if (isDead) return;
        isDead = true;

        Debug.Log("[ObjectiveHealthSystem] Objetivo destruído! Derrota.");

        // Derrota! Carregamento de cena
        if (isNGOSpawned && IsServer)
            NetworkManager.Singleton.SceneManager.LoadScene("Lose", LoadSceneMode.Single);
        else
            SceneManager.LoadScene("Lose");
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke();
    }
}
