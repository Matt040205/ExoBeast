// UpgradeTooltip.cs
using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class UpgradeTooltip : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    // Arraste os objetos do seu TooltipPanel para cá no Inspector
    public GameObject tooltipPanel;
    public TextMeshProUGUI upgradeNameText;
    public TextMeshProUGUI descriptionText;

    private string upgradeName;
    private string description;
    public bool IsVisible => tooltipPanel != null && tooltipPanel.activeInHierarchy;

    void Start()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    // Método para configurar as informações que este tooltip vai mostrar
    public void SetTooltipInfo(string newName, string newDescription)
    {
        upgradeName = newName;
        description = newDescription;

        if (IsVisible)
            ApplyTooltipText();
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (tooltipPanel != null && !string.IsNullOrEmpty(description))
        {
            ApplyTooltipText();
            tooltipPanel.SetActive(true);
        }
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    private void ApplyTooltipText()
    {
        if (upgradeNameText != null)
            upgradeNameText.text = upgradeName;

        if (descriptionText != null)
            descriptionText.text = description;
    }
}