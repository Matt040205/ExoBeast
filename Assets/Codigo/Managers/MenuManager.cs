using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using ExoBeasts.Managers;

/// <summary>
/// ── MenuManager ────────────────────────────────────────
/// Gerencia o menu principal, pause e navegacao de paineis de UI.
///
///  ▸ AbrirPause() / Resume(): pause visual-only (sem Time.timeScale)
///  ▸ ChangeScene(): encerra sessao NGO e carrega nova cena
///  ▸ Integra com GameModeManager para botoes Solo/Online
/// ─────────────────────────────────────────────────────
/// </summary>
public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;

    public GameObject menuPanel;
    public GameObject optionsPanel;
    public GameObject pausePanel;
    public GameObject hudPanel;
    public List<GameObject> pauseButtons = new List<GameObject>();
    public float optionsCenterX = -117f;

    [Header("Multiplayer")]
    [SerializeField] private Button botaoJogarSolo;
    [SerializeField] private Button botaoJogarOnline;

    private void Awake() => Instance = this;

    void Start()
    {
        if (optionsPanel) optionsPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);

        if (hudPanel != null) { if (menuPanel) menuPanel.SetActive(false); }
        else { if (menuPanel) menuPanel.SetActive(true); }

        if (botaoJogarSolo != null)
            botaoJogarSolo.onClick.AddListener(() => GameModeManager.Instance.StartSingleplayer());
        if (botaoJogarOnline != null)
            botaoJogarOnline.onClick.AddListener(() => GameModeManager.Instance.StartMultiplayer());
    }

    public void AbrirPause()
    {
        PauseControl.isPaused = true;
        if (hudPanel) hudPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(true);
        SetPauseButtonsState(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        PauseControl.isPaused = false;
        if (optionsPanel) optionsPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);
        if (hudPanel) hudPanel.SetActive(!BuildManager.isBuildingMode);
        Cursor.lockState = BuildManager.isBuildingMode ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = BuildManager.isBuildingMode;
    }

    public void Options()
    {
        StopAllCoroutines();
        StartCoroutine(ForcarAberturaOptions());
    }

    private IEnumerator ForcarAberturaOptions()
    {
        SetPauseButtonsState(false);

        yield return null;

        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);

            RectTransform rt = optionsPanel.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(optionsCenterX, 0f);
            }

            optionsPanel.transform.SetAsLastSibling();
        }

        Canvas.ForceUpdateCanvases();
    }

    public void BotaoBack()
    {
        if (optionsPanel) optionsPanel.SetActive(false);
        SetPauseButtonsState(true);
    }

    private void SetPauseButtonsState(bool state)
    {
        foreach (GameObject btn in pauseButtons)
        {
            if (btn != null) btn.SetActive(state);
        }
    }

    public void ChangeScene(string nomeDaCena)
    {
        PauseControl.isPaused = false;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        GameModeManager.LoadSceneSafe(nomeDaCena);
    }
}
