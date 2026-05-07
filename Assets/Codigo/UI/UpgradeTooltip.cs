// UpgradeTooltip.cs
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class UpgradeTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // Arraste os objetos do seu TooltipPanel para ca no Inspector
    public GameObject tooltipPanel;
    public TextMeshProUGUI upgradeNameText;
    public TextMeshProUGUI descriptionText;

    private string upgradeName;
    private string description;
    private bool isPointerInside;

    public bool IsPointerInside => isPointerInside;
    public bool IsVisible => tooltipPanel != null && tooltipPanel.activeInHierarchy && isPointerInside;

    void Start()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    // Metodo para configurar as informacoes que este tooltip vai mostrar
    public void SetTooltipInfo(string newName, string newDescription)
    {
        upgradeName = newName;
        description = newDescription;
        RefreshIfHovered();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        isPointerInside = true;
        if (tooltipPanel != null && !string.IsNullOrEmpty(description))
        {
            ApplyTooltipText();
            tooltipPanel.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        isPointerInside = false;
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    private void OnDisable()
    {
        isPointerInside = false;
    }

    public void RefreshIfHovered()
    {
        if (!isPointerInside || tooltipPanel == null || string.IsNullOrEmpty(description))
            return;

        ApplyTooltipText();
        tooltipPanel.SetActive(true);
    }

    private void ApplyTooltipText()
    {
        if (upgradeNameText != null)
            upgradeNameText.text = upgradeName;

        if (descriptionText != null)
            descriptionText.text = description;
    }
}
