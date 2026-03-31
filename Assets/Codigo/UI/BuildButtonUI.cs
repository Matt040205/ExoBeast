using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BuildButtonUI : MonoBehaviour
{
    public GameObject buildButtonPrefab;

    [Header("Contêineres das Lojas")]
    public Transform towerButtonContainer;
    public Transform trapButtonContainer;

    [Header("Configuração do Prefab (NOMES EXATOS)")]
    public string iconChildObjectName = "Icon";
    public string limitTextChildObjectName = "LimitText";
    public string priceTextChildObjectName = "PriceText";

    public void ClearTowerButtons()
    {
        if (towerButtonContainer == null) return;
        foreach (Transform child in towerButtonContainer) Destroy(child.gameObject);
    }

    public void ClearTrapButtons()
    {
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

            SetTextOnButton(buttonGO, limitTextChildObjectName, "", false);
            SetTextOnButton(buttonGO, priceTextChildObjectName, $"<color=#76D7C4>{towerData.cost}G</color>", true);

            Image iconImage = FindChildIcon(buttonGO);
            if (iconImage != null && towerData.characterIcon != null)
            {
                iconImage.sprite = towerData.characterIcon;
                iconImage.enabled = true;
            }

            if (button != null) button.onClick.AddListener(() => { BuildManager.Instance.SelectTowerToBuild(towerData); });
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

            bool showLimit = false;
            string limitString = "";

            if (trapData.buildLimit > 0 && BuildManager.Instance != null)
            {
                int currentCount = BuildManager.Instance.GetTrapCount(trapData);
                limitString = $"{currentCount}/{trapData.buildLimit}";
                showLimit = true;

                if (currentCount >= trapData.buildLimit) button.interactable = false;
            }

            SetTextOnButton(buttonGO, limitTextChildObjectName, limitString, showLimit);

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

            if (button != null && button.interactable) button.onClick.AddListener(() => { BuildManager.Instance.SelectTrapToBuild(trapData); });
        }
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

            // Checa se o nome é EXATAMENTE igual (ignorando espaços e maiúsculas)
            if (objName == searchString)
            {
                txt.text = textContent;
                txt.gameObject.SetActive(isActive);
                return;
            }
        }

        // SE O CÓDIGO CHEGOU AQUI, ELE NÃO ACHOU O TEXTO! Vamos dedurar o que tem lá dentro:
        string foundNames = "";
        foreach (var t in allTMPTexts) foundNames += $"[{t.gameObject.name}] ";

        Debug.LogError($"<color=red><b>[ERRO DE UI]</b></color> A HUD tentou atualizar o texto procurando pelo nome '{expectedName}', mas ele NÃO EXISTE dentro do seu Prefab de Botão!\n" +
                       $"Os únicos TextMeshPro que eu achei aí dentro foram: <b>{foundNames}</b>\n" +
                       $"<b>SOLUÇÃO:</b> Abra o Prefab do seu botão da loja e renomeie o objeto de texto para ficar EXATAMENTE igual a '{expectedName}'!");
    }
}