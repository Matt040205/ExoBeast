using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    public GameObject hudPanel;
    public GameObject pausePanel;
    public GameObject buildPanel;
    public BuildButtonUI buildButtonUI; // TEM QUE ESTAR PREENCHIDO!

    public TextMeshProUGUI timerText;

    public ObjectiveHealthSystem objectiveHealthSystem;
    public TextMeshProUGUI objectiveHealthText;
    public Image objectiveHealthBar;

    public Button towerShopButton;
    public Button trapShopButton;
    public GameObject towerShopPanel;
    public GameObject trapShopPanel;

    private float gameTime = 0f;
    private bool matchManagerFound = false;
    private float lastServerTime = -1f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    void Start()
    {
        ShowHUD();

        if (objectiveHealthSystem != null)
        {
            objectiveHealthSystem.OnHealthChanged += UpdateObjectiveHealthUI;
            UpdateObjectiveHealthUI();
        }

        if (towerShopButton != null) towerShopButton.onClick.AddListener(ShowTowerShop);
        if (trapShopButton != null) trapShopButton.onClick.AddListener(ShowTrapShop);

        if (buildPanel != null && buildPanel.activeInHierarchy)
        {
            ShowTowerShop();
        }
    }

    void Update()
    {
        if (HordeManager.Instance != null && !HordeManager.Instance.IsLocalMode)
        {
            var hm = HordeManager.Instance;

            if (!matchManagerFound)
            {
                matchManagerFound = true;
                // Registrar callback para quando o servidor manda um valor novos
                hm.currentMatchTime.OnValueChanged += OnServerTimeSynced;
                Debug.Log($"[UIManager] HordeManager encontrado! MatchTime={hm.currentMatchTime.Value:F1}s. Registrando callback de sync.");
            }

            float serverTime = hm.currentMatchTime.Value;

            if (serverTime != lastServerTime)
            {
                // Servidor mandou um valor novo — resincronizar
                gameTime = serverTime;
                lastServerTime = serverTime;
            }
            else
            {
                // Entre ticks de rede: predição local para manter o timer suave
                gameTime += Time.deltaTime;
            }
        }
        else
        {
            // Fallback local (singleplayer ou antes do HordeManager spawnar)
            gameTime += Time.deltaTime;
        }

        UpdateTimerDisplay(gameTime);
    }

    private void OnServerTimeSynced(float oldVal, float newVal)
    {
        // Quando chega valor novo do servidor, forçar resync
        gameTime = newVal;
        lastServerTime = newVal;
    }

    /// <summary>
    /// Chamado externamente (ex: MatchManager.OnNetworkSpawn) para forçar sync imediato.
    /// </summary>
    public void ForceTimerSync(float serverTime)
    {
        Debug.Log($"[UIManager] ForceTimerSync chamado! serverTime={serverTime:F1}s, gameTime anterior={gameTime:F1}s");
        gameTime = serverTime;
        lastServerTime = serverTime;
        matchManagerFound = true;
        UpdateTimerDisplay(gameTime);
    }

    private void OnDestroy()
    {
        if (objectiveHealthSystem != null) objectiveHealthSystem.OnHealthChanged -= UpdateObjectiveHealthUI;
    }

    public void UpdateObjectiveHealthUI()
    {
        if (objectiveHealthSystem == null) return;
        float currentHealth = objectiveHealthSystem.currentHealth.Value;
        float maxHealth = objectiveHealthSystem.maxHealth;

        if (objectiveHealthText != null) objectiveHealthText.text = $"{currentHealth:F0} / {maxHealth:F0}";

        if (objectiveHealthBar != null)
            objectiveHealthBar.fillAmount = maxHealth > 0 ? currentHealth / maxHealth : 0;
    }

    public void UpdateBuildUI(List<CharacterBase> towers, List<TrapDataSO> traps)
    {
        // O ALARME SE FALTAR A REFERÊNCIA NO UIMANAGER
        if (buildButtonUI == null)
        {
            Debug.LogError("<b>[UIManager]</b> ERRO CRÍTICO: O campo 'Build Button UI' está VAZIO no Inspector do UI Manager! Arraste a script pra lá!");
            return;
        }

        buildButtonUI.ClearTowerButtons();
        buildButtonUI.CreateTowerBuildButtons(towers);

        buildButtonUI.ClearTrapButtons();
        buildButtonUI.CreateTrapBuildButtons(traps);

        WireBuildTooltips();
    }

    public void ShowHUD()
    {
        if (hudPanel != null) hudPanel.SetActive(true);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (buildPanel != null) buildPanel.SetActive(false);
    }

    public void ShowPauseMenu(bool show)
    {
        if (pausePanel != null) pausePanel.SetActive(show);

        if (show)
        {
            if (hudPanel != null) hudPanel.SetActive(false);
        }
        else
        {
            if (BuildManager.isBuildingMode) ShowBuildUI(true);
            else ShowHUD();
        }
    }

    public void ShowBuildUI(bool show)
    {
        if (buildPanel != null) buildPanel.SetActive(show);
        if (show)
        {
            if (hudPanel != null) hudPanel.SetActive(false);
            ShowTowerShop();
        }
        else ShowHUD();
    }

    public void ShowTowerShop()
    {
        if (towerShopPanel != null) towerShopPanel.SetActive(true);
        if (trapShopPanel != null) trapShopPanel.SetActive(false);

        if (towerShopButton != null) towerShopButton.interactable = false;
        if (trapShopButton != null) trapShopButton.interactable = true;
    }

    public void ShowTrapShop()
    {
        if (towerShopPanel != null) towerShopPanel.SetActive(false);
        if (trapShopPanel != null) trapShopPanel.SetActive(true);

        if (towerShopButton != null) towerShopButton.interactable = true;
        if (trapShopButton != null) trapShopButton.interactable = false;
    }

    public void UpdateTimerDisplay(float timeInSeconds)
    {
        if (timerText == null) return;
        int minutes = Mathf.FloorToInt(timeInSeconds / 60);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60);
        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
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
            if (tooltipTrigger == null) continue;

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
            : value.Replace(" ", "").Trim().ToLowerInvariant();
    }
}
