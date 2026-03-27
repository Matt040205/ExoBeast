using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;
using ExoBeasts.Managers;

/// <summary>
/// ── MenuManager ────────────────────────────────────────
/// Gerencia o menu principal, pause e navegação de painéis de UI.
/// Inclui agora funções de rede para iniciar Host, Client e Server.
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

        // Mantendo suas lógicas de GameModeManager
        if (botaoJogarSolo != null)
            botaoJogarSolo.onClick.AddListener(() => GameModeManager.Instance.StartSingleplayer());
        if (botaoJogarOnline != null)
            botaoJogarOnline.onClick.AddListener(() => GameModeManager.Instance.StartMultiplayer());
    }

    #region Funções de Conexão (NGO)

    /// <summary>
    /// Inicia o jogo como HOST (Servidor e Jogador ao mesmo tempo).
    /// </summary>
    public void HostGame()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.StartHost();
            Debug.Log("[MenuManager] Iniciando como HOST...");
            // Se você quiser carregar a cena do mapa logo após dar host:
            // NetworkManager.Singleton.SceneManager.LoadScene("NomeDoSeuMapa", LoadSceneMode.Single);
        }
    }

    /// <summary>
    /// Inicia o jogo como CLIENT (Tenta conectar a um IP configurado no Unity Transport).
    /// </summary>
    public void JoinGame()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.StartClient();
            Debug.Log("[MenuManager] Tentando conectar como CLIENTE...");
        }
    }

    /// <summary>
    /// Inicia apenas o SERVIDOR (Sem jogador local).
    /// </summary>
    public void ServerOnly()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.StartServer();
            Debug.Log("[MenuManager] Iniciando apenas como SERVIDOR...");
        }
    }

    #endregion

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