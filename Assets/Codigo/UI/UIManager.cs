using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using ExoBeasts.Multiplayer.GameServer;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;

    public GameObject hudPanel;
    public GameObject pausePanel;
    public GameObject buildPanel;
    public BuildButtonUI buildButtonUI;

    public TextMeshProUGUI timerText;
    public TextMeshProUGUI objectiveHealthText;
    public Image objectiveHealthBar;

    public Button towerShopButton;
    public Button trapShopButton;
    public GameObject towerShopPanel;
    public GameObject trapShopPanel;

    private float gameTime;
    private bool matchManagerFound;
    private float lastServerTime = -1f;
    private float objectiveCurrentHealth;
    private float objectiveMaxHealth = 1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        ShowHUD();

        ObjectiveHealthBus.OnObjectiveHealthChanged += OnObjectiveHealthChanged;
        if (ObjectiveHealthBus.TryGetLastKnown(out float currentHealth, out float maxHealth))
            OnObjectiveHealthChanged(currentHealth, maxHealth);

        if (towerShopButton != null)
            towerShopButton.onClick.AddListener(ShowTowerShop);

        if (trapShopButton != null)
            trapShopButton.onClick.AddListener(ShowTrapShop);

        if (buildPanel != null && buildPanel.activeInHierarchy)
            ShowTowerShop();
    }

    private void Update()
    {
        // Timer agora vem do MatchManager (canonico). HordeManager.IsLocalMode segue como
        // fonte de verdade para "estamos em rede ou local" porque o HordeManager existe
        // tanto em singleplayer quanto multiplayer, enquanto o MatchManager so existe em rede.
        bool isNetworkMatch = HordeManager.Instance != null &&
                              !HordeManager.Instance.IsLocalMode &&
                              MatchManager.Instance != null;

        if (isNetworkMatch)
        {
            MatchManager matchManager = MatchManager.Instance;

            if (!matchManagerFound)
            {
                matchManagerFound = true;
                matchManager.MatchTime.OnValueChanged += OnServerTimeSynced;
            }

            // Dead-reckoning: extrapola localmente entre snapshots autoritativos.
            // O servidor agora publica MatchTime apenas a cada 1s, mas o display
            // continua suave porque incrementamos com Time.deltaTime entre updates.
            float serverTime = matchManager.MatchTime.Value;
            if (serverTime != lastServerTime)
            {
                gameTime = serverTime;
                lastServerTime = serverTime;
            }
            else
            {
                gameTime += Time.deltaTime;
            }
        }
        else
        {
            gameTime += Time.deltaTime;
        }

        UpdateTimerDisplay(gameTime);
    }

    private void OnDestroy()
    {
        ObjectiveHealthBus.OnObjectiveHealthChanged -= OnObjectiveHealthChanged;

        if (matchManagerFound && MatchManager.Instance != null)
            MatchManager.Instance.MatchTime.OnValueChanged -= OnServerTimeSynced;
    }

    public void ForceTimerSync(float serverTime)
    {
        gameTime = serverTime;
        lastServerTime = serverTime;
        matchManagerFound = true;
        UpdateTimerDisplay(gameTime);
    }

    public void UpdateObjectiveHealthUI()
    {
        if (objectiveHealthText != null)
            objectiveHealthText.text = $"{objectiveCurrentHealth:F0} / {objectiveMaxHealth:F0}";

        if (objectiveHealthBar != null)
            objectiveHealthBar.fillAmount = objectiveMaxHealth > 0f ? objectiveCurrentHealth / objectiveMaxHealth : 0f;
    }

    public void UpdateBuildUI(List<CharacterBase> towers, List<TrapDataSO> traps)
    {
        if (buildButtonUI == null)
        {
            Debug.LogError("<b>[UIManager]</b> O campo 'Build Button UI' nao foi preenchido no Inspector.");
            return;
        }

        buildButtonUI.ClearTowerButtons();
        buildButtonUI.CreateTowerBuildButtons(towers);

        buildButtonUI.ClearTrapButtons();
        buildButtonUI.CreateTrapBuildButtons(traps);
        buildButtonUI.RefreshTrapAvailability(traps);

        WireBuildTooltips();
    }

    public void RefreshTrapBuildUI(List<TrapDataSO> traps)
    {
        if (buildButtonUI == null)
            return;

        buildButtonUI.RefreshTrapAvailability(traps);
    }

    public void ShowHUD()
    {
        EnsurePanelHierarchyActive(hudPanel);

        if (hudPanel != null)
            hudPanel.SetActive(true);

        if (pausePanel != null)
            pausePanel.SetActive(false);

        if (buildPanel != null)
            buildPanel.SetActive(false);
    }

    public void ShowPauseMenu(bool show)
    {
        if (show)
            EnsurePanelHierarchyActive(pausePanel);

        if (pausePanel != null)
            pausePanel.SetActive(show);

        if (show)
        {
            if (hudPanel != null)
                hudPanel.SetActive(false);
        }
        else
        {
            if (BuildManager.isBuildingMode)
                ShowBuildUI(true);
            else
                ShowHUD();
        }
    }

    public void ShowBuildUI(bool show)
    {
        if (show)
            EnsurePanelHierarchyActive(buildPanel);

        if (buildPanel != null)
            buildPanel.SetActive(show);

        if (show)
        {
            if (hudPanel != null)
                hudPanel.SetActive(false);

            ShowTowerShop();

            if (BuildManager.Instance != null)
            {
                // Se a UI ainda não tem botões de armadilha (ex: cena recém-carregada antes do
                // SetAvailableTowers chegar), recria toda a UI de build em vez de só dar refresh.
                // Sem esse fallback, RefreshTrapAvailability sai cedo (binding vazio) e os
                // botões nunca aparecem mesmo que o BuildManager tenha dados disponíveis.
                if (buildButtonUI != null && !buildButtonUI.HasTrapButtons && GameDataManager.Instance != null)
                    BuildManager.Instance.SetAvailableTowers(GameDataManager.Instance.equipeSelecionada);
                else
                    RefreshTrapBuildUI(BuildManager.Instance.availableTraps);
            }
        }
        else
        {
            ShowHUD();
        }
    }

    public void ShowTowerShop()
    {
        if (towerShopPanel != null)
            towerShopPanel.SetActive(true);

        if (trapShopPanel != null)
            trapShopPanel.SetActive(false);

        if (towerShopButton != null)
            towerShopButton.interactable = false;

        if (trapShopButton != null)
            trapShopButton.interactable = true;
    }

    public void ShowTrapShop()
    {
        if (towerShopPanel != null)
            towerShopPanel.SetActive(false);

        if (trapShopPanel != null)
            trapShopPanel.SetActive(true);

        if (towerShopButton != null)
            towerShopButton.interactable = true;

        if (trapShopButton != null)
            trapShopButton.interactable = false;
    }

    public void UpdateTimerDisplay(float timeInSeconds)
    {
        if (timerText == null)
            return;

        int minutes = Mathf.FloorToInt(timeInSeconds / 60f);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60f);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }

    private void OnServerTimeSynced(float oldValue, float newValue)
    {
        gameTime = newValue;
        lastServerTime = newValue;
    }

    private void OnObjectiveHealthChanged(float currentHealth, float maxHealth)
    {
        objectiveCurrentHealth = currentHealth;
        objectiveMaxHealth = Mathf.Max(maxHealth, 1f);
        UpdateObjectiveHealthUI();
    }

    private void WireBuildTooltips()
    {
        BuildTooltipTrigger[] tooltipTriggers = GetComponentsInChildren<BuildTooltipTrigger>(true);
        if (tooltipTriggers == null || tooltipTriggers.Length == 0)
            return;

        Transform tooltipPanelTransform = FindChildByNormalizedName(transform, "tooltippanel");
        TextMeshProUGUI nomeTarget = FindTextByNormalizedName(transform, "nomedabuild");
        TextMeshProUGUI descricaoTarget = FindTextByNormalizedName(transform, "descricaodabuild");

        foreach (BuildTooltipTrigger tooltipTrigger in tooltipTriggers)
        {
            if (tooltipTrigger == null)
                continue;

            if (tooltipTrigger.tooltipPanel == null && tooltipPanelTransform != null)
                tooltipTrigger.tooltipPanel = tooltipPanelTransform.gameObject;

            if (tooltipTrigger.nomeText == null)
                tooltipTrigger.nomeText = nomeTarget;

            if (tooltipTrigger.descricaoText == null)
                tooltipTrigger.descricaoText = descricaoTarget;
        }
    }

    private Transform FindChildByNormalizedName(Transform root, string normalizedName)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (NormalizeName(child.name) == normalizedName)
                return child;
        }

        return null;
    }

    private TextMeshProUGUI FindTextByNormalizedName(Transform root, string normalizedName)
    {
        foreach (TextMeshProUGUI text in root.GetComponentsInChildren<TextMeshProUGUI>(true))
        {
            if (NormalizeName(text.gameObject.name) == normalizedName)
                return text;
        }

        return null;
    }

    private string NormalizeName(string value)
    {
        return string.IsNullOrEmpty(value)
            ? string.Empty
            : value.Replace(" ", string.Empty).Trim().ToLowerInvariant();
    }

    private void EnsurePanelHierarchyActive(GameObject panel)
    {
        if (panel == null)
            return;

        Transform parent = panel.transform.parent;
        while (parent != null)
        {
            if (!parent.gameObject.activeSelf)
            {
                Debug.LogWarning(
                    $"[UIManager] Reativando '{parent.name}' para exibir painel '{panel.name}'.",
                    parent.gameObject);
                parent.gameObject.SetActive(true);
            }

            parent = parent.parent;
        }
    }
}
