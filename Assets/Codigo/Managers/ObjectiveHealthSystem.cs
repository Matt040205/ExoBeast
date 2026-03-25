using UnityEngine;
using System;
using UnityEngine.SceneManagement;
using Unity.Netcode;

/// <summary>
/// ── ObjectiveHealthSystem ──────────────────────────────
/// Vida do objetivo (cristal/base) com autoridade no servidor.
///
///  ▸ NetworkVariable currentHealth: sincroniza vida para todos
///  ▸ Server: TakeDamage, Die → carrega cena Lose via NGO SceneManager
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

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    private void OnCurrentHealthChanged(float oldVal, float newVal) => OnHealthChanged?.Invoke();

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

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
        base.OnNetworkDespawn();
    }

    public void TakeDamage(float damage)
    {
        if (!IsServer) return;
        if (currentHealth.Value <= 0 || isDead) return;

        currentHealth.Value = Mathf.Max(currentHealth.Value - damage, 0);
        NotifyHealthChanged();

        if (currentHealth.Value <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        if (!IsServer) return;
        if (isDead) return;
        isDead = true;

        // Derrota! Carregamento de cena em rede via Servidor
        NetworkManager.Singleton.SceneManager.LoadScene("Lose", LoadSceneMode.Single);
    }

    private void NotifyHealthChanged()
    {
        OnHealthChanged?.Invoke();
    }
}
