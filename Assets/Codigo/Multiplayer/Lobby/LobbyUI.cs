using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ExoBeasts.Multiplayer.Lobby
{
    /// <summary>
    /// Gerencia a interface de usuario do sistema de lobby
    /// </summary>
    public class LobbyUI : MonoBehaviour
    {
        [Header("Panels")]
        [SerializeField] private GameObject createLobbyPanel;
        [SerializeField] private GameObject lobbyListPanel;
        [SerializeField] private GameObject lobbyRoomPanel;

        [Header("Create Lobby Panel")]
        [SerializeField] private TMP_InputField lobbyNameInput;
        [SerializeField] private Slider maxPlayersSlider;
        [SerializeField] private TMP_Text maxPlayersText;
        [SerializeField] private Toggle isPublicToggle;
        [SerializeField] private Button createButton;

        [Header("Lobby List Panel")]
        [SerializeField] private Transform lobbyListContent;
        [SerializeField] private GameObject lobbyItemPrefab;
        [SerializeField] private Button refreshButton;
        [SerializeField] private Button createNewButton;

        [Header("Lobby Room Panel")]
        [SerializeField] private TMP_Text lobbyNameText;
        [SerializeField] private Transform playerSlotsContainer;
        [SerializeField] private Button selectCharacterButton;
        [SerializeField] private Toggle readyToggle;
        [SerializeField] private Button startGameButton;
        [SerializeField] private Button leaveLobbyButton;

        [Header("Status")]
        [SerializeField] private TMP_Text statusText;

        private void Start()
        {
            SetupUI();
            SubscribeToEvents();
            ShowLobbyListPanel();
        }

        private void SetupUI()
        {
            // Setup Create Lobby Panel
            if (maxPlayersSlider != null)
            {
                maxPlayersSlider.minValue = 2;
                maxPlayersSlider.maxValue = 4;
                maxPlayersSlider.value = 4;
                maxPlayersSlider.onValueChanged.AddListener(OnMaxPlayersChanged);
            }

            // Setup buttons
            if (createButton != null)
                createButton.onClick.AddListener(OnCreateLobbyClicked);
            if (refreshButton != null)
                refreshButton.onClick.AddListener(OnRefreshClicked);
            if (createNewButton != null)
                createNewButton.onClick.AddListener(ShowCreateLobbyPanel);
            if (selectCharacterButton != null)
                selectCharacterButton.onClick.AddListener(OnSelectCharacterClicked);
            if (readyToggle != null)
                readyToggle.onValueChanged.AddListener(OnReadyToggled);
            if (startGameButton != null)
                startGameButton.onClick.AddListener(OnStartGameClicked);
            if (leaveLobbyButton != null)
                leaveLobbyButton.onClick.AddListener(OnLeaveLobbyClicked);
        }

        private void SubscribeToEvents()
        {
            var lobbyManager = LobbyManager.Instance;
            lobbyManager.OnLobbyCreated += OnLobbyCreated;
            lobbyManager.OnLobbiesFound += OnLobbiesFound;
            lobbyManager.OnLobbyJoined += OnLobbyJoined;
            lobbyManager.OnLobbyLeft += OnLobbyLeft;
            lobbyManager.OnError += OnError;
        }

        private void OnDestroy()
        {
            var lobbyManager = LobbyManager.Instance;
            if (lobbyManager != null)
            {
                lobbyManager.OnLobbyCreated -= OnLobbyCreated;
                lobbyManager.OnLobbiesFound -= OnLobbiesFound;
                lobbyManager.OnLobbyJoined -= OnLobbyJoined;
                lobbyManager.OnLobbyLeft -= OnLobbyLeft;
                lobbyManager.OnError -= OnError;
            }
        }

        // Panel Management
        private void ShowCreateLobbyPanel()
        {
            createLobbyPanel?.SetActive(true);
            lobbyListPanel?.SetActive(false);
            lobbyRoomPanel?.SetActive(false);
        }

        private void ShowLobbyListPanel()
        {
            createLobbyPanel?.SetActive(false);
            lobbyListPanel?.SetActive(true);
            lobbyRoomPanel?.SetActive(false);
        }

        private void ShowLobbyRoomPanel()
        {
            createLobbyPanel?.SetActive(false);
            lobbyListPanel?.SetActive(false);
            lobbyRoomPanel?.SetActive(true);
        }

        // Button Callbacks
        private void OnCreateLobbyClicked()
        {
            var settings = new LobbySettings
            {
                lobbyName = lobbyNameInput != null ? lobbyNameInput.text : "Nova Sala",
                maxPlayers = maxPlayersSlider != null ? (int)maxPlayersSlider.value : 4,
                isPublic = isPublicToggle != null ? isPublicToggle.isOn : true
            };

            LobbyManager.Instance.CreateLobby(settings);
            SetStatusText("Criando lobby...");
        }

        private void OnRefreshClicked()
        {
            var filter = new LobbySearchFilter();
            LobbyManager.Instance.SearchLobbies(filter);
            SetStatusText("Buscando lobbies...");
        }

        private void OnSelectCharacterClicked()
        {
            // TODO: Abrir tela de selecao de personagem
            Debug.Log("[LobbyUI] Selecionar personagem");
        }

        private void OnReadyToggled(bool isReady)
        {
            LobbyManager.Instance.SetReady(isReady);
        }

        private void OnStartGameClicked()
        {
            LobbyManager.Instance.StartMatch();
            SetStatusText("Iniciando partida...");
        }

        private void OnLeaveLobbyClicked()
        {
            LobbyManager.Instance.LeaveLobby();
        }

        private void OnMaxPlayersChanged(float value)
        {
            if (maxPlayersText != null)
                maxPlayersText.text = value.ToString("0");
        }

        // Event Handlers
        private void OnLobbyCreated(LobbyInfo lobby)
        {
            SetStatusText($"Lobby criado: {lobby.lobbyName}");
            ShowLobbyRoomPanel();
            UpdateLobbyRoomUI(lobby);
        }

        private void OnLobbiesFound(System.Collections.Generic.List<LobbyInfo> lobbies)
        {
            SetStatusText($"Encontrados {lobbies.Count} lobbies");
            UpdateLobbyList(lobbies);
        }

        private void OnLobbyJoined(LobbyInfo lobby)
        {
            SetStatusText($"Entrou no lobby: {lobby.lobbyName}");
            ShowLobbyRoomPanel();
            UpdateLobbyRoomUI(lobby);
        }

        private void OnLobbyLeft()
        {
            SetStatusText("Saiu do lobby");
            ShowLobbyListPanel();
        }

        private void OnError(string error)
        {
            SetStatusText($"Erro: {error}");
        }

        // UI Updates
        private void UpdateLobbyList(System.Collections.Generic.List<LobbyInfo> lobbies)
        {
            // TODO: Limpar lista existente
            // TODO: Instanciar prefabs para cada lobby
            // TODO: Configurar callbacks de click
        }

        private void UpdateLobbyRoomUI(LobbyInfo lobby)
        {
            if (lobbyNameText != null)
                lobbyNameText.text = lobby.lobbyName;

            // TODO: Atualizar slots de jogadores
            // TODO: Habilitar/desabilitar botao Start (apenas para host)
        }

        private void SetStatusText(string message)
        {
            if (statusText != null)
            {
                statusText.text = message;
                Debug.Log($"[LobbyUI] {message}");
            }
        }
    }
}
