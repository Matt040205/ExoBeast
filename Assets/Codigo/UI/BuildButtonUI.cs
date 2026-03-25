using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using static Unity.VisualScripting.Member;

public class BuildButtonUI : MonoBehaviour
{
    public GameObject buildButtonPrefab;

    [Header("Contêineres das Lojas")]
    public Transform towerButtonContainer;
    public Transform trapButtonContainer;

    [Header("Configuração do Prefab")]
    [Tooltip("O nome EXATO do GameObject 'Filho' que contém a imagem do ícone")]
    public string iconChildObjectName = "Icon";
    [Tooltip("O nome EXATO do GameObject 'Filho' que contém o texto do limite")]
    public string limitTextChildObjectName = "LimitText";
    [Tooltip("O nome EXATO do GameObject 'Filho' que contém o texto do preço")]
    public string priceTextChildObjectName = "PriceText";

    public void ClearTowerButtons()
    {
        if (towerButtonContainer == null) { Debug.LogError("DEBUG FALHA: 'Tower Button Container' está NULO!"); return; }

        foreach (Transform child in towerButtonContainer)
        {
            Destroy(child.gameObject);
        }
    }

    public void ClearTrapButtons()
    {
        if (trapButtonContainer == null) { Debug.LogError("DEBUG FALHA: 'Trap Button Container' está NULO!"); return; }

        foreach (Transform child in trapButtonContainer)
        {
            Destroy(child.gameObject);
        }
    }

    public void CreateTowerBuildButtons(List<CharacterBase> availableTowers)
    {
        if (towerButtonContainer == null) { Debug.LogError("DEBUG FALHA: 'Tower Button Container' está NULO!"); return; }
        if (buildButtonPrefab == null) { Debug.LogError("DEBUG FALHA: 'Build Button Prefab' está NULO!"); return; }

        foreach (CharacterBase towerData in availableTowers)
        {
            if (towerData == null) continue;
            GameObject buttonGO = Instantiate(buildButtonPrefab, towerButtonContainer);
            Button button = buttonGO.GetComponent<Button>();

            BuildTooltipTrigger tooltipTrigger = buttonGO.GetComponent<BuildTooltipTrigger>();
            if (tooltipTrigger != null)
            {
                tooltipTrigger.SetBuildInfo(towerData.name, towerData.description);
            }

            TextMeshProUGUI limitText = FindChildText(buttonGO, limitTextChildObjectName);
            if (limitText != null)
            {
                limitText.gameObject.SetActive(false);
            }

            TextMeshProUGUI priceText = FindChildText(buttonGO, priceTextChildObjectName);
            if (priceText != null)
            {
                priceText.text = $"<color=#76D7C4>{towerData.cost}G</color>";
                priceText.gameObject.SetActive(true);
            }

            Image iconImage = FindChildIcon(buttonGO);
            if (iconImage != null && towerData.characterIcon != null)
            {
                iconImage.sprite = towerData.characterIcon;
                iconImage.enabled = true;
            }
            else
            {
                Debug.LogWarning($"Não foi possível encontrar o 'Image' no objeto filho chamado '{iconChildObjectName}' no prefab {buttonGO.name}", buttonGO);
            }

            if (button != null)
            {
                button.onClick.AddListener(() => {
                    BuildManager.Instance.SelectTowerToBuild(towerData);
                });
            }
        }
    }

    public void CreateTrapBuildButtons(List<TrapDataSO> availableTraps)
    {
        if (trapButtonContainer == null) { Debug.LogError("DEBUG FALHA: 'Trap Button Container' está NULO!"); return; }
        if (buildButtonPrefab == null) { Debug.LogError("DEBUG FALHA: 'Build Button Prefab' está NULO!"); return; }

        foreach (TrapDataSO trapData in availableTraps)
        {
            if (trapData == null) continue;
            GameObject buttonGO = Instantiate(buildButtonPrefab, trapButtonContainer);
            Button button = buttonGO.GetComponent<Button>();

            BuildTooltipTrigger tooltipTrigger = buttonGO.GetComponent<BuildTooltipTrigger>();
            if (tooltipTrigger != null)
            {
                tooltipTrigger.SetBuildInfo(trapData.trapName, trapData.description);
            }

            TextMeshProUGUI limitText = FindChildText(buttonGO, limitTextChildObjectName);
            if (trapData.buildLimit > 0 && BuildManager.Instance != null)
            {
                int currentCount = BuildManager.Instance.GetTrapCount(trapData);

                if (limitText != null)
                {
                    limitText.text = $"{currentCount}/{trapData.buildLimit}";
                    limitText.gameObject.SetActive(true);
                }

                if (currentCount >= trapData.buildLimit)
                {
                    button.interactable = false;
                }
            }
            else if (limitText != null)
            {
                limitText.gameObject.SetActive(false);
            }

            TextMeshProUGUI priceText = FindChildText(buttonGO, priceTextChildObjectName);
            if (priceText != null)
            {
                List<string> costs = new List<string>();
                if (trapData.geoditeCost > 0) costs.Add($"<color=#76D7C4>{trapData.geoditeCost}G</color>");
                if (trapData.darkEtherCost > 0) costs.Add($"<color=#C39BD3>{trapData.darkEtherCost}E</color>");
                priceText.text = costs.Count > 0 ? string.Join(" / ", costs) : "Grátis";
                priceText.gameObject.SetActive(true);
            }

            Image iconImage = FindChildIcon(buttonGO);
            if (iconImage != null && trapData.icon != null)
            {
                iconImage.sprite = trapData.icon;
                iconImage.enabled = true;
            }
            else
            {
                Debug.LogWarning($"Não foi possível encontrar o 'Image' no objeto filho chamado '{iconChildObjectName}' no prefab {buttonGO.name}", buttonGO);
            }

            if (button != null && button.interactable)
            {
                button.onClick.AddListener(() => {
                    BuildManager.Instance.SelectTrapToBuild(trapData);
                });
            }
        }
    }

    private Image FindChildIcon(GameObject buttonGO)
    {
        Image[] allImages = buttonGO.GetComponentsInChildren<Image>(true);
        foreach (Image img in allImages)
        {
            if (img.gameObject.name == iconChildObjectName)
            {
                return img;
            }
        }
        return null;
    }

    private TextMeshProUGUI FindChildText(GameObject buttonGO, string objectName)
    {
        TextMeshProUGUI[] allTexts = buttonGO.GetComponentsInChildren<TextMeshProUGUI>(true);
        foreach (TextMeshProUGUI txt in allTexts)
        {
            if (txt.gameObject.name == objectName)
            {
                return txt;
            }
        }
        Debug.LogWarning($"Não foi possível encontrar o 'TextMeshProUGUI' no objeto filho chamado '{objectName}' no prefab {buttonGO.name}", buttonGO);
        return null;
    }
}