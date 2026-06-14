using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildButtonUI : MonoBehaviour
{
    private sealed class TrapButtonBinding
    {
        public TrapButtonBinding(GameObject buttonObject, Button button, TrapDataSO trapData)
        {
            ButtonObject = buttonObject;
            Button = button;
            TrapData = trapData;
        }

        public GameObject ButtonObject { get; }
        public Button Button { get; }
        public TrapDataSO TrapData { get; }
    }

    public GameObject buildButtonPrefab;

    [Header("Contêineres das Lojas")]
    public Transform towerButtonContainer;
    public Transform trapButtonContainer;

    [Header("Configuração do Prefab (NOMES EXATOS)")]
    public string iconChildObjectName = "Icon";
    public string limitTextChildObjectName = "LimitText";
    public string priceTextChildObjectName = "PriceText";

    private readonly Dictionary<TrapDataSO, TrapButtonBinding> trapButtonBindings = new Dictionary<TrapDataSO, TrapButtonBinding>();
    private List<TrapDataSO> _lastAvailableTraps;

    public bool HasTrapButtons => trapButtonBindings.Count > 0;

    public void ClearTowerButtons()
    {
        if (towerButtonContainer == null) return;
        foreach (Transform child in towerButtonContainer) Destroy(child.gameObject);
    }

    public void ClearTrapButtons()
    {
        trapButtonBindings.Clear();

        if (trapButtonContainer == null) return;
        foreach (Transform child in trapButtonContainer) Destroy(child.gameObject);
    }

    public void CreateTowerBuildButtons(List<CharacterBase> availableTowers)
    {
        if (towerButtonContainer == null || buildButtonPrefab == null) return;

        foreach (CharacterBase towerData in availableTowers)
        {
            if (towerData == null) continue;
            GameObject buttonGO = Instantiate(buildButtonPrefab, towerButtonContainer);
            Button button = buttonGO.GetComponent<Button>();

            BuildTooltipTrigger tooltipTrigger = buttonGO.GetComponent<BuildTooltipTrigger>();
            if (tooltipTrigger != null) tooltipTrigger.SetBuildInfo(towerData.name, towerData.description);

            int characterIndex = BuildManager.Instance != null ? BuildManager.Instance.GetCharacterLibraryIndex(towerData) : -1;
            bool isBuilt = false;
            if (BuildManager.Instance != null && characterIndex >= 0)
            {
                isBuilt = BuildManager.Instance.IsCharacterAlreadyBuilt(characterIndex);
            }

            string limitStr = "";
            bool limitActive = false;
            bool canBuild = true;

            if (isBuilt)
            {
                limitStr = "Em campo";
                limitActive = true;
                canBuild = false;
            }

            SetTextOnButton(buttonGO, limitTextChildObjectName, limitStr, limitActive);
            SetTextOnButton(buttonGO, priceTextChildObjectName, $"<color=#76D7C4>{towerData.cost}G</color>", true);

            Image iconImage = FindChildIcon(buttonGO);
            if (iconImage != null && towerData.characterIcon != null)
            {
                iconImage.sprite = towerData.characterIcon;
                iconImage.enabled = true;
            }

            if (button != null)
            {
                button.interactable = canBuild;
                button.onClick.AddListener(() => { BuildManager.Instance.SelectTowerToBuild(towerData); });
            }
        }
    }

    public void CreateTrapBuildButtons(List<TrapDataSO> availableTraps)
    {
        if (trapButtonContainer == null || buildButtonPrefab == null) return;

        foreach (TrapDataSO trapData in availableTraps)
        {
            if (trapData == null) continue;
            GameObject buttonGO = Instantiate(buildButtonPrefab, trapButtonContainer);
            Button button = buttonGO.GetComponent<Button>();

            BuildTooltipTrigger tooltipTrigger = buttonGO.GetComponent<BuildTooltipTrigger>();
            if (tooltipTrigger != null) tooltipTrigger.SetBuildInfo(trapData.trapName, trapData.description);

            List<string> costs = new List<string>();
            if (trapData.geoditeCost > 0) costs.Add($"<color=#76D7C4>{trapData.geoditeCost}G</color>");
            if (trapData.darkEtherCost > 0) costs.Add($"<color=#C39BD3>{trapData.darkEtherCost}E</color>");
            string priceString = costs.Count > 0 ? string.Join(" / ", costs) : "Grátis";

            SetTextOnButton(buttonGO, priceTextChildObjectName, priceString, true);

            Image iconImage = FindChildIcon(buttonGO);
            if (iconImage != null && trapData.icon != null)
            {
                iconImage.sprite = trapData.icon;
                iconImage.enabled = true;
            }

            TrapDataSO capturedTrapData = trapData;
            if (button != null)
                button.onClick.AddListener(() => { BuildManager.Instance.SelectTrapToBuild(capturedTrapData); });

            trapButtonBindings[capturedTrapData] = new TrapButtonBinding(buttonGO, button, capturedTrapData);
            UpdateTrapButtonAvailability(trapButtonBindings[capturedTrapData]);
        }

        // Re-aplica o último estado de disponibilidade conhecido — cobre updates que chegaram
        // ANTES dos botões existirem (race entre UpdateTrapCountsClientRpc e abertura do menu).
        if (_lastAvailableTraps != null)
            ApplyTrapAvailabilityToBindings(_lastAvailableTraps);
    }

    public void RefreshTrapAvailability(List<TrapDataSO> availableTraps)
    {
        if (availableTraps == null)
            return;

        // Armazena SEMPRE o último estado, mesmo se botões ainda não existem.
        // CreateTrapBuildButtons re-aplica este snapshot ao final, garantindo convergência da UI.
        _lastAvailableTraps = availableTraps;

        if (trapButtonBindings.Count == 0)
            return;

        ApplyTrapAvailabilityToBindings(availableTraps);
    }

    private void ApplyTrapAvailabilityToBindings(List<TrapDataSO> availableTraps)
    {
        foreach (TrapDataSO trapData in availableTraps)
        {
            if (trapData == null || !trapButtonBindings.TryGetValue(trapData, out TrapButtonBinding binding))
                continue;

            UpdateTrapButtonAvailability(binding);
        }
    }

    private void UpdateTrapButtonAvailability(TrapButtonBinding binding)
    {
        if (binding == null || binding.TrapData == null || binding.ButtonObject == null)
            return;

        bool showLimit = false;
        string limitString = "";
        bool canBuild = true;

        if (binding.TrapData.buildLimit > 0 && BuildManager.Instance != null)
        {
            int currentCount = BuildManager.Instance.GetTrapCount(binding.TrapData);
            limitString = $"{currentCount}/{binding.TrapData.buildLimit}";
            showLimit = true;
            canBuild = currentCount < binding.TrapData.buildLimit;
        }

        SetTextOnButton(binding.ButtonObject, limitTextChildObjectName, limitString, showLimit);

        if (binding.Button != null)
            binding.Button.interactable = canBuild;
    }

    private Image FindChildIcon(GameObject buttonGO)
    {
        Image[] allImages = buttonGO.GetComponentsInChildren<Image>(true);
        string searchString = iconChildObjectName.Replace(" ", "").ToLower();

        foreach (Image img in allImages)
        {
            string objName = img.gameObject.name.Replace(" ", "").ToLower();
            if (objName == searchString) return img;
        }
        return null;
    }

    // =================================================================
    // A LUPA DEDO-DURO: Acha o texto ou grita no Console!
    // =================================================================
    private void SetTextOnButton(GameObject buttonGO, string expectedName, string textContent, bool isActive)
    {
        string searchString = expectedName.Replace(" ", "").ToLower();
        TextMeshProUGUI[] allTMPTexts = buttonGO.GetComponentsInChildren<TextMeshProUGUI>(true);

        foreach (TextMeshProUGUI txt in allTMPTexts)
        {
            string objName = txt.gameObject.name.Replace(" ", "").ToLower();

            // Checa se o nome e EXATAMENTE igual (ignorando espacos e maiusculas)
            if (objName == searchString)
            {
                txt.text = textContent;
                txt.gameObject.SetActive(isActive);
                return;
            }
        }

        // SE O CODIGO CHEGOU AQUI, ELE NAO ACHOU O TEXTO! Vamos dedurar o que tem la dentro:
        string foundNames = "";
        foreach (var t in allTMPTexts) foundNames += $"[{t.gameObject.name}] ";

        Debug.LogError($"<color=red><b>[ERRO DE UI]</b></color> A HUD tentou atualizar o texto procurando pelo nome '{expectedName}', mas ele NAO EXISTE dentro do seu Prefab de Botao!\n" +
                       $"Os unicos TextMeshPro que eu achei ai dentro foram: <b>{foundNames}</b>\n" +
                       $"<b>SOLUCAO:</b> Abra o Prefab do seu botao da loja e renomeie o objeto de texto para ficar EXATAMENTE igual a '{expectedName}'!");
    }
}
