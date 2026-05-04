using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class TutorialPopupUI : MonoBehaviour
{
    [Header("Referencia ao painel filho que sera ligado/desligado")]
    public GameObject tutorialPopupPanel;

    [Header("Elementos de UI dentro do painel")]
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public Button closeButton;

    // Callback executado quando o jogador fecha o tutorial
    private System.Action onCloseCallback;

    private void Awake()
    {
        if (tutorialPopupPanel == null && transform.childCount > 0)
            tutorialPopupPanel = transform.GetChild(0).gameObject;

        if (tutorialPopupPanel != null)
        {
            if (titleText == null)
                titleText = tutorialPopupPanel.GetComponentInChildren<TextMeshProUGUI>(true);
            if (closeButton == null)
                closeButton = tutorialPopupPanel.GetComponentInChildren<Button>(true);
            if (descriptionText == null)
            {
                TextMeshProUGUI[] allTexts = tutorialPopupPanel.GetComponentsInChildren<TextMeshProUGUI>(true);
                if (allTexts.Length >= 2) descriptionText = allTexts[1];
            }
        }

        if (closeButton != null)
            closeButton.onClick.AddListener(Close);

        if (tutorialPopupPanel != null)
            tutorialPopupPanel.SetActive(false);
    }

    public void Show(TutorialData data, System.Action onClose = null)
    {
        if (tutorialPopupPanel == null) return;

        onCloseCallback = onClose;
        tutorialPopupPanel.SetActive(true);

        if (titleText != null) titleText.text = data.titulo;
        if (descriptionText != null) descriptionText.text = data.descricao;

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Close()
    {
        if (tutorialPopupPanel != null)
            tutorialPopupPanel.SetActive(false);

        string cenaAtual = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        if (cenaAtual == "CenaMapaNOVO" && !BuildManager.isBuildingMode)
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        // Executa o callback DEPOIS de fechar (tutorial encadeado)
        System.Action cb = onCloseCallback;
        onCloseCallback = null;
        cb?.Invoke();
    }
}
