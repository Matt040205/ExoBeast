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
                rt.anchoredPosition = new Vector2(optionsCenterX, 0f);

            optionsPanel.transform.SetAsLastSibling();
        }

        Canvas.ForceUpdateCanvases();
    }

    public void BotaoBack()
    {
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

        bool isMenuDestination = nomeDaCena.ToLower().Contains("menu");
        bool isSelectionDestination = nomeDaCena.ToLower().Contains("escolherpersonagem");

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

        if (botaoJogarSolo != null)
        {
            botaoJogarSolo.onClick = new Button.ButtonClickedEvent();
            botaoJogarSolo.onClick.AddListener(() => GameModeManager.EnsureInstance().StartSingleplayer());
        }
        else
        {
            Debug.LogError("[MenuManager] Nao foi possivel encontrar o botao 'Singleplayer' na MenuScene.");
        }

        if (botaoJogarOnline != null)
        {
            botaoJogarOnline.onClick = new Button.ButtonClickedEvent();
            botaoJogarOnline.onClick.AddListener(() => GameModeManager.EnsureInstance().StartMultiplayer());
        }
        else
        {
            Debug.LogError("[MenuManager] Nao foi possivel encontrar o botao 'Multiplayer' na MenuScene.");
        }
    }

    private void TryAutoBindButton(ref Button button, string expectedName)
    {
        if (button != null)
            return;

        foreach (Button candidate in FindObjectsOfType<Button>(true))
        {
            if (candidate != null &&
                candidate.gameObject.scene == gameObject.scene &&
                candidate.gameObject.name == expectedName)
            {
                button = candidate;
                Debug.LogWarning($"[MenuManager] Botao '{expectedName}' nao estava serializado na MenuScene. Referencia reconstituida automaticamente por nome.");
                return;
            }
        }
    }
}
