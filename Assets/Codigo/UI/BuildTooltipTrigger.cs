using UnityEngine;
using UnityEngine.EventSystems;
using TMPro;

public class BuildTooltipTrigger : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Referências do Tooltip")]
    public GameObject tooltipPanel;
    public TextMeshProUGUI nomeText;
    public TextMeshProUGUI descricaoText;

    private string nome;
    private string descricao;

    void Start()
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }

    public void SetBuildInfo(string newName, string newDescription)
    {
        nome = newName;
        descricao = newDescription;
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        // --- DIAGNÓSTICO ADICIONADO ---

        if (tooltipPanel == null)
        {
            Debug.LogError("BuildTooltipTrigger FALHOU: O campo 'Tooltip Panel' está NULO (None) no Inspector do prefab!");
            return;
        }

        if (string.IsNullOrEmpty(descricao))
        {
            Debug.LogError("BuildTooltipTrigger FALHOU: A 'descricao' está vazia. Verifique se o ScriptableObject (TrapDataSO ou CharacterBase) tem uma descrição preenchida.");
            return;
        }

        // Se chegou aqui, está tudo correto.
        if (nomeText != null) nomeText.text = nome;
        if (descricaoText != null) descricaoText.text = descricao;
        tooltipPanel.SetActive(true);
        tooltipPanel.transform.SetAsLastSibling();
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        if (tooltipPanel != null)
        {
            tooltipPanel.SetActive(false);
        }
    }
}