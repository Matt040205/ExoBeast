using UnityEngine;
using TMPro;
using Unity.Netcode;

public enum CurrencyType
{
    Geodites,
    DarkEther
}

/// <summary>
/// ── CurrencyManager ────────────────────────────────────
/// Economia compartilhada entre todos os jogadores (Geodites e Dark Ether).
///
///  ▸ NetworkVariables: networkedGeodites, networkedDarkEther (Server write)
///  ▸ AddCurrency / SpendCurrency: rota para ServerRpc se chamado por cliente
///  ▸ OnValueChanged: atualiza UI local em todos os clientes
///  ▸ Propriedades CurrentGeodites/CurrentDarkEther para compatibilidade
/// ─────────────────────────────────────────────────────
/// </summary>
public class CurrencyManager : NetworkBehaviour
{
    public static CurrencyManager Instance { get; private set; }

    [Header("Network Sync")]
    public NetworkVariable<int> networkedGeodites = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> networkedDarkEther = new NetworkVariable<int>(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Header("Referências da UI")]
    public TextMeshProUGUI geoditesTextBuild;
    public TextMeshProUGUI geoditesText;
    public TextMeshProUGUI darkEtherText;

    [Header("Valores Iniciais")]
    [SerializeField] private int initialGeodites = 500;
    [SerializeField] private int initialDarkEther = 0;

    public int CurrentGeodites => networkedGeodites.Value;
    public int CurrentDarkEther => networkedDarkEther.Value;

    private bool jaGanhouRecursoTutorial = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this); // Apenas destrói o script duplicado, protegendo o resto do objeto!
            return;
        }
        Instance = this;
    }


    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            networkedGeodites.Value = initialGeodites;
            networkedDarkEther.Value = initialDarkEther;
        }

        networkedGeodites.OnValueChanged += (oldVal, newVal) => UpdateUI();
        networkedDarkEther.OnValueChanged += (oldVal, newVal) => UpdateUI();

        UpdateUI();
    }

    public void AddCurrency(int amount, CurrencyType type)
    {
        if (amount <= 0) return;

        if (IsServer)
        {
            ApplyAddCurrencyServer(amount, type);
        }
        else
        {
            RequestAddCurrencyServerRpc(amount, type);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestAddCurrencyServerRpc(int amount, CurrencyType type)
    {
        ApplyAddCurrencyServer(amount, type);
    }

    private void ApplyAddCurrencyServer(int amount, CurrencyType type)
    {
        if (type == CurrencyType.Geodites)
            networkedGeodites.Value += amount;
        else
            networkedDarkEther.Value += amount;

        if (!jaGanhouRecursoTutorial && GameDataManager.Instance != null && GameDataManager.Instance.tutoriaisConcluidos.Contains("USE_SKILLS"))
        {
            jaGanhouRecursoTutorial = true;
            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.TriggerTutorial("EXPLAIN_UPGRADE");
            }
        }
    }

    public bool HasEnoughCurrency(int amount, CurrencyType type)
    {
        if (type == CurrencyType.Geodites)
            return networkedGeodites.Value >= amount;
        else
            return networkedDarkEther.Value >= amount;
    }

    public void SpendCurrency(int amount, CurrencyType type)
    {
        if (IsServer)
        {
            ApplySpendCurrencyServer(amount, type);
        }
        else
        {
            RequestSpendCurrencyServerRpc(amount, type);
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSpendCurrencyServerRpc(int amount, CurrencyType type)
    {
        ApplySpendCurrencyServer(amount, type);
    }

    private void ApplySpendCurrencyServer(int amount, CurrencyType type)
    {
        if (HasEnoughCurrency(amount, type))
        {
            if (type == CurrencyType.Geodites)
                networkedGeodites.Value -= amount;
            else
                networkedDarkEther.Value -= amount;
        }
    }

    private void UpdateUI()
    {
        if (geoditesText != null)
        {
            geoditesText.text = $"{networkedGeodites.Value}";
        }

        if (geoditesTextBuild != null)
        {
            geoditesTextBuild.text = $" {networkedGeodites.Value}";
        }

        if (darkEtherText != null)
        {
            darkEtherText.text = $" {networkedDarkEther.Value}";
        }
    }
}
