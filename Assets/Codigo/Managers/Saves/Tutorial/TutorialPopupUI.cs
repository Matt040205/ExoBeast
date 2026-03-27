using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// ── TutorialPopupUI ─────────────────────────────────────
/// Exibe popup de tutorial com titulo e descricao (UI local, sem rede).
///
///  ▸ Show(TutorialData): preenche textos, ativa objeto, solta o cursor
///  ▸ Close: desativa objeto, trava cursor (exceto na cena Menu)
///  ▸ MonoBehaviour — UI display apenas, sem logica de rede necessaria
/// ─────────────────────────────────────────────────────
/// </summary>
public class TutorialPopupUI : MonoBehaviour
{
    public TextMeshProUGUI titleText;
    public TextMeshProUGUI descriptionText;
    public Button closeButton;

    private void Awake()
    {
        if (closeButton != null)
            closeButton.onClick.AddListener(Close);
    }

    public void Show(TutorialData data)
    {
        if (titleText != null) titleText.text = data.titulo;
        if (descriptionText != null) descriptionText.text = data.descricao;
        
        gameObject.SetActive(true);
        // Em multiplayer, a pausa deve ser logica e individual. Para simplificar, abrimos o cursor apenas.
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Close()
    {
        gameObject.SetActive(false);
        // Verificar se nao estamos em Menu antes de travar o cursor
        if (UnityEngine.SceneManagement.SceneManager.GetActiveScene().name != "MenuScene")
        {
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }
    }
}
