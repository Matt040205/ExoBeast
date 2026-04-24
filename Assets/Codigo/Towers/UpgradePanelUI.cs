using TMPro;
using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using ExoBeasts.Multiplayer.Sync;

public class UpgradePanelUI : MonoBehaviour
{
    [Header("Referências Gerais")]
    public GameObject uiPanel;
    public UnityEngine.UI.Image towerImage;

    [Header("Referências do Caminho 1 (DPS)")]
    public Button upgradeButton1;
    public TextMeshProUGUI costText1;
    public TextMeshProUGUI levelText1;

    [Header("Referências do Caminho 2 (Controle)")]
    public Button upgradeButton2;
    public TextMeshProUGUI costText2;
    public TextMeshProUGUI levelText2;

    [Header("Referências do Caminho 3 (Suporte)")]
    public Button upgradeButton3;
    public TextMeshProUGUI costText3;
    public TextMeshProUGUI levelText3;

    [Header("Controles da Torre")]
    public Button sellButton;
    public TextMeshProUGUI sellPriceText;
    public float sellRefundPercentage = 0.4f;

    private TowerController currentTower;
    private NetworkedBuilding currentNetworkedBuilding;
    private CanvasGroup panelCanvasGroup;
    private const int MAX_TOTAL_POINTS = 6;
    private int lastUiStateHash = int.MinValue;

    void Awake()
    {
        panelCanvasGroup = uiPanel.GetComponent<CanvasGroup>();
        if (panelCanvasGroup == null)
        {
            Debug.LogError("CanvasGroup não encontrado no uiPanel! Adicione um componente CanvasGroup.", uiPanel);
            panelCanvasGroup = uiPanel.AddComponent<CanvasGroup>();
        }

        if (sellButton != null)
        {
            sellButton.onClick.AddListener(SellTower);
        }

        TryResolveUpgradeTooltipTargets();
    }

    void Start()
    {
        HidePanel();
    }

    void Update()
    {
        if (currentTower == null || uiPanel == null || !uiPanel.activeInHierarchy)
            return;

        int currentHash = CaptureUiStateHash();
        if (currentHash == lastUiStateHash)
            return;

        lastUiStateHash = currentHash;
        UpdatePanelInfo();
    }

    public void ShowPanel(TowerController tower)
    {
        currentTower = tower;
        currentNetworkedBuilding = tower != null ? tower.GetComponent<NetworkedBuilding>() : null;
        lastUiStateHash = int.MinValue;
        TryResolveUpgradeTooltipTargets();
        uiPanel.SetActive(true);

        if (towerImage != null && currentTower.towerData.characterIcon != null)
        {
            towerImage.sprite = currentTower.towerData.characterIcon;
            ResetTowerImageAlpha();
        }

        UpdatePanelInfo();
    }

    public void HidePanel()
    {
        currentTower = null;
        currentNetworkedBuilding = null;
        lastUiStateHash = int.MinValue;
        uiPanel.SetActive(false);
    }

    public bool IsPanelVisible()
    {
        return uiPanel.activeSelf;
    }

    // --- NOVA FUNÇÃO AUXILIAR: Pega o nível atual do sistema de habilidades ---
    private int GetCurrentLevel(int pathIndex)
    {
        if (currentTower == null) return 0;
        if (currentTower.currentPathLevels != null && pathIndex < currentTower.currentPathLevels.Length) { return currentTower.currentPathLevels[pathIndex]; } return 0;
    }

    // --- NOVA FUNÇÃO AUXILIAR: Converte Index para Enum ---
    private TowerPath GetPathEnum(int pathIndex)
    {
        switch (pathIndex)
        {
            case 0: return TowerPath.DPS;
            case 1: return TowerPath.Control;
            case 2: return TowerPath.Support;
            default: return TowerPath.None;
        }
    }

    void UpdatePanelInfo()
    {
        if (currentTower == null || panelCanvasGroup == null) return;

        // Calcula o total de pontos gastos somando os níveis atuais
        int totalPointsSpent = 0;
        for (int i = 0; i < 3; i++) totalPointsSpent += GetCurrentLevel(i);

        bool isFullyUpgraded = totalPointsSpent >= MAX_TOTAL_POINTS;
        bool canInteractLocally = currentNetworkedBuilding == null || currentNetworkedBuilding.CanInteractLocally();

        UpdatePathButton(0, upgradeButton1, costText1, levelText1, isFullyUpgraded, totalPointsSpent, canInteractLocally);
        UpdatePathButton(1, upgradeButton2, costText2, levelText2, isFullyUpgraded, totalPointsSpent, canInteractLocally);
        UpdatePathButton(2, upgradeButton3, costText3, levelText3, isFullyUpgraded, totalPointsSpent, canInteractLocally);

        if (sellPriceText != null)
        {
            int refundAmount = Mathf.FloorToInt(currentTower.TotalCostSpent * sellRefundPercentage);
            sellPriceText.text = $"Vender por <color=#76D7C4>{refundAmount}G</color>";
        }

        panelCanvasGroup.interactable = !isFullyUpgraded && canInteractLocally;
        panelCanvasGroup.alpha = (!isFullyUpgraded && canInteractLocally) ? 1f : 0.5f;

        ResetTowerImageAlpha();
    }

    void UpdatePathButton(int pathIndex, Button button, TextMeshProUGUI costText, TextMeshProUGUI levelText, bool isFullyUpgraded, int totalPointsSpent, bool canInteractLocally)
    {
        UpgradeTooltip tooltip = button.GetComponent<UpgradeTooltip>();

        if (pathIndex >= currentTower.towerData.upgradePaths.Count || currentTower.towerData.upgradePaths[pathIndex] == null)
        {
            button.gameObject.SetActive(false);
            if (costText) costText.gameObject.SetActive(false);
            if (levelText) levelText.gameObject.SetActive(false);
            return;
        }
        else
        {
            button.gameObject.SetActive(true);
            if (costText) costText.gameObject.SetActive(true);
            if (levelText) levelText.gameObject.SetActive(true);
        }

        // Usa a nova função auxiliar
        int currentLevel = GetCurrentLevel(pathIndex);
        UpgradePath path = currentTower.towerData.upgradePaths[pathIndex];
        int maxLevelThisPath = path.upgradesInPath.Count;

        // Conta quantos caminhos têm nível > 0
        int pathsChosenCount = 0;
        for (int i = 0; i < 3; i++) if (GetCurrentLevel(i) > 0) pathsChosenCount++;

        bool anotherPathIsTier3Plus = false;
        // Verifica se algum OUTRO caminho é tier 3 ou mais
        for (int i = 0; i < 3; i++)
        {
            if (i != pathIndex && GetCurrentLevel(i) > 2)
            {
                anotherPathIsTier3Plus = true;
                break;
            }
        }

        bool isLockedByChoice = false;
        string lockReason = "";

        // Lógica de bloqueio (Pode ajustar conforme sua regra de jogo)
        // Se já escolheu 2 caminhos e este ainda é 0, bloqueia
        /* // Comentei essa lógica pois na árvore mista geralmente você pode pegar um pouco de tudo,
        // mas se quiser manter a regra de "Max 2 caminhos", descomente abaixo:
        
        if (pathsChosenCount >= 2 && currentLevel == 0)
        {
            isLockedByChoice = true;
            lockReason = "Só é possível escolher dois caminhos por torre.";
        }
        */

        levelText.text = $"Nível {currentLevel}/{maxLevelThisPath}";
        string tooltipTitleBase = path.pathName ?? "Caminho Desconhecido";
        string tooltipDescription = "";

        SetElementAlpha(button.GetComponent<UnityEngine.UI.Image>(), 1f);
        if (costText) costText.color = new UnityEngine.Color(costText.color.r, costText.color.g, costText.color.b, 1f);
        if (levelText) levelText.color = new UnityEngine.Color(levelText.color.r, levelText.color.g, levelText.color.b, 1f);

        if (isLockedByChoice)
        {
            button.interactable = false;
            costText.text = "";
            levelText.text = "Bloqueado";
            tooltipDescription = lockReason;
            if (tooltip != null) tooltip.SetTooltipInfo($"{tooltipTitleBase} (BLOQUEADO)", tooltipDescription);
            if (!isFullyUpgraded)
            {
                SetElementAlpha(button.GetComponent<UnityEngine.UI.Image>(), 0.5f);
                if (costText) costText.color = new UnityEngine.Color(costText.color.r, costText.color.g, costText.color.b, 0.5f);
                if (levelText) levelText.color = new UnityEngine.Color(levelText.color.r, levelText.color.g, levelText.color.b, 0.5f);
            }
            return;
        }

        bool isMaxLevelForPath = currentLevel >= maxLevelThisPath;

        if (!isMaxLevelForPath)
        {
            Upgrade nextUpgrade = path.upgradesInPath[currentLevel];
            int geoditeCost = nextUpgrade.geoditeCost;
            int darkEtherCost = nextUpgrade.darkEtherCost;

            string costString = "";
            List<string> costs = new List<string>();
            if (geoditeCost > 0) costs.Add($"<color=#76D7C4>{geoditeCost}G</color>");
            if (darkEtherCost > 0) costs.Add($"<color=#C39BD3>{darkEtherCost}E</color>");
            costString = costs.Count > 0 ? string.Join(" / ", costs) : "Grátis";

            bool canAfford = CurrencyManager.Instance.HasEnoughCurrency(geoditeCost, CurrencyType.Geodites) &&
                             CurrencyManager.Instance.HasEnoughCurrency(darkEtherCost, CurrencyType.DarkEther);

            button.interactable = canInteractLocally && canAfford && !isFullyUpgraded;
            costText.text = !isFullyUpgraded ? costString : "";

            tooltipDescription = $"<b>Próximo Nível ({currentLevel + 1}): {nextUpgrade.upgradeName}</b>\n{nextUpgrade.description}";
            if (tooltip != null) tooltip.SetTooltipInfo(tooltipTitleBase, tooltipDescription);
        }
        else
        {
            button.interactable = false;
            costText.text = "";
            levelText.text = "Nível MAX";
            tooltipDescription = "Este caminho já está no nível máximo.";
            if (tooltip != null) tooltip.SetTooltipInfo($"{tooltipTitleBase} (NÍVEL MÁXIMO)", tooltipDescription);
        }
    }

    void SetElementAlpha(Graphic element, float alpha)
    {
        if (element != null)
        {
            UnityEngine.Color color = element.color;
            color.a = alpha;
            element.color = color;
        }
    }

    void ResetTowerImageAlpha()
    {
        if (towerImage != null)
        {
            UnityEngine.Color imgColor = towerImage.color;
            imgColor.a = 1f;
            towerImage.color = imgColor;
        }
    }

    public void UpgradePath(int pathIndex)
    {
        if (currentTower == null) return;
        if (currentNetworkedBuilding != null && currentNetworkedBuilding.IsSpawned)
        {
            currentNetworkedBuilding.RequestUpgradeServerRpc(pathIndex);
            if (TutorialManager.Instance != null)
                TutorialManager.Instance.TriggerTutorial("NEXT_WAVE");

            StartCoroutine(RefreshUIAfterFrame());
            return;
        }

        if (pathIndex >= currentTower.towerData.upgradePaths.Count || currentTower.towerData.upgradePaths[pathIndex] == null) return;

        UpgradePath path = currentTower.towerData.upgradePaths[pathIndex];

        // Pega o nível atual usando a nova função
        int currentLevel = GetCurrentLevel(pathIndex);

        if (currentLevel >= path.upgradesInPath.Count) return;

        // Calcula total gasto usando a nova função
        int totalPointsSpent = 0;
        for (int i = 0; i < 3; i++) totalPointsSpent += GetCurrentLevel(i);

        if (totalPointsSpent >= MAX_TOTAL_POINTS)
        {
            Debug.Log("Limite total de upgrades (6) atingido para esta torre!");
            return;
        }

        Upgrade nextUpgrade = path.upgradesInPath[currentLevel];
        int geoditeCost = nextUpgrade.geoditeCost;
        int darkEtherCost = nextUpgrade.darkEtherCost;

        if (CurrencyManager.Instance.HasEnoughCurrency(geoditeCost, CurrencyType.Geodites) &&
            CurrencyManager.Instance.HasEnoughCurrency(darkEtherCost, CurrencyType.DarkEther))
        {
            CurrencyManager.Instance.SpendCurrency(geoditeCost, CurrencyType.Geodites);
            CurrencyManager.Instance.SpendCurrency(darkEtherCost, CurrencyType.DarkEther);

            // Determina qual ENUM passar para o Controller (DPS, Control, Support)
            TowerPath pathEnum = GetPathEnum(pathIndex);

            // CORREÇÃO: Passa o pathEnum para o ApplyUpgrade
            currentTower.ApplyUpgrade(nextUpgrade, geoditeCost, darkEtherCost, pathEnum);

            // REMOVIDO: currentTower.currentPathLevels[pathIndex]++; 
            // (O sistema PaintAbilitySystem faz o incremento internamente agora)

            if (TutorialManager.Instance != null)
            {
                TutorialManager.Instance.TriggerTutorial("NEXT_WAVE");
            }

            StartCoroutine(RefreshUIAfterFrame());
        }
        else
        {
            Debug.Log("Recursos insuficientes para este upgrade!");
        }
    }

    private void SellTower()
    {
        if (currentTower == null) return;

        if (currentNetworkedBuilding != null && currentNetworkedBuilding.IsSpawned)
        {
            currentNetworkedBuilding.RequestSellServerRpc(sellRefundPercentage);
            HidePanel();
            return;
        }

        currentTower.SellTower(sellRefundPercentage);

        HidePanel();
    }

    private int CaptureUiStateHash()
    {
        if (currentTower == null)
            return 0;

        int hash = currentTower.TotalCostSpent;
        for (int i = 0; i < 3; i++)
            hash = (hash * 31) + GetCurrentLevel(i);

        return hash;
    }

    private void TryResolveUpgradeTooltipTargets()
    {
        Transform tooltipPanelTransform = FindChildByNormalizedName(transform, "tooltippanel");
        TextMeshProUGUI upgradeNameTarget = FindTextByNormalizedName(transform, "nomedocaminho");
        TextMeshProUGUI descriptionTarget = FindTextByNormalizedName(transform, "descricaodocaminho");

        UpgradeTooltip[] tooltips = GetComponentsInChildren<UpgradeTooltip>(true);
        foreach (UpgradeTooltip tooltip in tooltips)
        {
            if (tooltip == null) continue;
            if (tooltip.tooltipPanel == null && tooltipPanelTransform != null)
                tooltip.tooltipPanel = tooltipPanelTransform.gameObject;

            if (tooltip.upgradeNameText == null)
                tooltip.upgradeNameText = upgradeNameTarget;

            if (tooltip.descriptionText == null)
                tooltip.descriptionText = descriptionTarget;
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

    private IEnumerator RefreshUIAfterFrame()
    {
        yield return new WaitForEndOfFrame();
        if (currentTower != null && uiPanel.activeInHierarchy)
        {
            UpdatePanelInfo();
            if (uiPanel != null)
            {
                LayoutRebuilder.ForceRebuildLayoutImmediate(uiPanel.GetComponent<RectTransform>());
            }
        }
    }
}
