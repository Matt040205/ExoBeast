using System.Collections;
using System.Collections.Generic;
using ExoBeasts.Managers;
using ExoBeasts.Multiplayer.Core;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Gerencia menu principal, pause e navegacao de UI.
/// Mantem atalhos legados de rede e blinda a transicao entre fluxos.
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
    [SerializeField] private Button botaoOptions;
    [SerializeField] private Button botaoCreditos;

    private bool _sceneChangeInProgress;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        if (optionsPanel) optionsPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);

        if (hudPanel != null)
        {
            if (menuPanel) menuPanel.SetActive(false);
        }
        else
        {
            if (menuPanel) menuPanel.SetActive(true);
        }

        if (SceneManager.GetActiveScene().name == "MenuScene")
            BindMainMenuButtons();
    }

    #region Funcoes de Conexao (NGO)

    public void HostGame()
    {
        if (NetworkManager.Singleton == null) return;

        if (ExoBeasts.Multiplayer.Core.MppmHelper.IsClone)
        {
            Debug.LogWarning("[MenuManager] HostGame() ignorado porque este processo e um clone MPPM.");
            return;
        }

        if (NetworkManager.Singleton.IsListening)
        {
            Debug.LogWarning("[MenuManager] HostGame() ignorado porque o NetworkManager ja esta em execucao.");
            return;
        }

        NetworkManager.Singleton.StartHost();
        Debug.Log("[MenuManager] Iniciando como HOST...");
    }

    public void JoinGame()
    {
        if (NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.StartClient();
            Debug.Log("[MenuManager] Tentando conectar como CLIENTE...");
        }
    }

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
        // MenuScene: usa transição animada via pilha de navegação.
        if (MenuTabSlider.Instance != null)
        {
            MenuTabSlider.Instance.NavigateTo("Options");
            return;
        }

        // Fallback legado (PauseMenu sem TabSlider)
        StopAllCoroutines();
        StartCoroutine(ForcarAberturaOptions());
    }

    /// <summary>Abre o painel de créditos com slide animado.</summary>
    public void Creditos()
    {
        if (MenuTabSlider.Instance != null)
        {
            MenuTabSlider.Instance.NavigateTo("Credits");
            return;
        }
        Debug.LogWarning("[MenuManager] MenuTabSlider não encontrado. Fallback sem animação.");
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
                rt.anchoredPosition = new Vector2(optionsCenterX, 0f);

            optionsPanel.transform.SetAsLastSibling();
        }

        Canvas.ForceUpdateCanvases();
    }

    public void BotaoBack()
    {
        // MenuScene: usa Back() da pilha de navegação animada.
        if (MenuTabSlider.Instance != null)
        {
            MenuTabSlider.Instance.Back();
            return;
        }

        // Comportamento legado para PauseMenu
        if (optionsPanel) optionsPanel.SetActive(false);
        SetPauseButtonsState(true);
    }

    public void ChangeScene(string nomeDaCena)
    {
        if (_sceneChangeInProgress)
            return;

        StartCoroutine(ChangeSceneRoutine(nomeDaCena));
    }

    private IEnumerator ChangeSceneRoutine(string nomeDaCena)
    {
        _sceneChangeInProgress = true;
        PauseControl.isPaused = false;

        if (SceneFader.Instance != null)
        {
            yield return SceneFader.Instance.FadeOutRoutine();
        }
        else
        {
            var go = new GameObject("SceneFader");
            var fader = go.AddComponent<SceneFader>();
            yield return fader.FadeOutRoutine();
        }

        bool isMenuDestination = nomeDaCena.ToLower().Contains("menu");
        bool isSelectionDestination = nomeDaCena.ToLower().Contains("escolherpersonagem") || nomeDaCena.ToLower().Contains("cenasele");

        if (isMenuDestination && GameDataManager.Instance != null)
        {
            GameDataManager.Instance.LimparSelecao();
            GameDataManager.Instance.SaveGame();
        }

        if (isMenuDestination || isSelectionDestination)
        {
            yield return MultiplayerRuntimeReset.ResetToOfflineLocal();
        }
        else if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening)
        {
            NetworkManager.Singleton.Shutdown();
        }

        _sceneChangeInProgress = false;
        SceneManager.LoadScene(nomeDaCena);
    }

    private void SetPauseButtonsState(bool state)
    {
        foreach (GameObject btn in pauseButtons)
        {
            if (btn != null) btn.SetActive(state);
        }
    }

    private void BindMainMenuButtons()
    {
        TryAutoBindButton(ref botaoJogarSolo, "Singleplayer");
        TryAutoBindButton(ref botaoJogarOnline, "Multiplayer");
        TryAutoBindButton(ref botaoOptions, "Options", "options");
        TryAutoBindButton(ref botaoCreditos, "Credits", "Creditos");

        if (botaoJogarSolo != null)
        {
            botaoJogarSolo.onClick = new Button.ButtonClickedEvent();
            botaoJogarSolo.onClick.AddListener(() => GameModeManager.EnsureInstance().StartSingleplayer());
        }

        if (botaoJogarOnline != null)
        {
            botaoJogarOnline.onClick = new Button.ButtonClickedEvent();
            botaoJogarOnline.onClick.AddListener(() => GameModeManager.EnsureInstance().StartMultiplayer());
        }

        if (botaoOptions != null)
        {
            botaoOptions.onClick = new Button.ButtonClickedEvent();
            botaoOptions.onClick.AddListener(() => Options());
        }

        if (botaoCreditos != null)
        {
            botaoCreditos.onClick = new Button.ButtonClickedEvent();
            botaoCreditos.onClick.AddListener(() => Creditos());
        }

        BindBackButtonsInPanels();
    }

    private void BindBackButtonsInPanels()
    {
        foreach (Button btn in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (btn != null && btn.gameObject.scene == gameObject.scene && IsBackButtonName(btn.gameObject.name))
            {
                btn.onClick = new Button.ButtonClickedEvent();
                btn.onClick.AddListener(BotaoBack);
            }
        }
    }

    private static bool IsBackButtonName(string objectName)
    {
        string normalized = (objectName ?? "").Trim();
        return normalized.Equals("Back", System.StringComparison.OrdinalIgnoreCase) ||
               normalized.IndexOf("voltar", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private void TryAutoBindButton(ref Button button, string expectedName)
    {
        if (button != null)
            return;

        foreach (Button candidate in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate != null &&
                candidate.gameObject.scene == gameObject.scene &&
                candidate.gameObject.name.Trim().Equals(expectedName.Trim(), System.StringComparison.OrdinalIgnoreCase))
            {
                button = candidate;
                Debug.LogWarning($"[MenuManager] Botao '{expectedName}' nao estava serializado na MenuScene. Referencia reconstituida automaticamente por nome.");
                return;
            }
        }
    }

    /// <summary>
    /// Overload que tenta o nome principal primeiro, depois o fallback.
    /// Útil para botões que podem ter nomes em PT ou EN.
    /// </summary>
    private void TryAutoBindButton(ref Button button, string primaryName, string fallbackName)
    {
        TryAutoBindButton(ref button, primaryName);
        if (button == null)
            TryAutoBindButton(ref button, fallbackName);
    }
}

