using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ExoBeasts.Multiplayer.Auth;

namespace ExoBeasts.Multiplayer.Lobby
{
    /// <summary>
    /// ── LobbyUI ──────────────────────────────────────────
    /// Interface Canvas para o sistema de lobby (aguarda artes finais).
    ///
    ///  ▸ Tres paineis: CreateLobby, LobbyList, LobbyRoom
    ///  ▸ UpdateLobbyList: recria itens usando LobbyItemUI prefab
    ///  ▸ RefreshPlayerSlots: reconstroi slots com TextMeshProUGUI dinamico
    ///  ▸ Inscrita nos eventos do LobbyManager; desinscreve em OnDestroy
    /// ─────────────────────────────────────────────────────
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
            if (maxPlayersSlider != null)
            {
                maxPlayersSlider.minValue = 2;
                maxPlayersSlider.maxValue = 4;
                maxPlayersSlider.value = 4;
                maxPlayersSlider.onValueChanged.AddListener(OnMaxPlayersChanged);
            }

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
            lobbyManager.OnLobbyCreated  += OnLobbyCreated;
            lobbyManager.OnLobbiesFound  += OnLobbiesFound;
            lobbyManager.OnLobbyJoined   += OnLobbyJoined;
            lobbyManager.OnLobbyLeft     += OnLobbyLeft;
            lobbyManager.OnMemberJoined  += OnMemberJoined;
            lobbyManager.OnMemberLeft    += OnMemberLeft;
            lobbyManager.OnError         += OnError;
        }

        private void OnDestroy()
        {
            var lobbyManager = LobbyManager.Instance;
            if (lobbyManager != null)
            {
                lobbyManager.OnLobbyCreated  -= OnLobbyCreated;
                lobbyManager.OnLobbiesFound  -= OnLobbiesFound;
                lobbyManager.OnLobbyJoined   -= OnLobbyJoined;
                lobbyManager.OnLobbyLeft     -= OnLobbyLeft;
                lobbyManager.OnMemberJoined  -= OnMemberJoined;
                lobbyManager.OnMemberLeft    -= OnMemberLeft;
                lobbyManager.OnError         -= OnError;
            }
        }

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

        private void OnMemberJoined(LobbyMember member)
        {
            SetStatusText($"{member.displayName} entrou na sala");
            RefreshPlayerSlots();
        }

        private void OnMemberLeft(LobbyMember member)
        {
            SetStatusText($"{member.displayName} saiu da sala");
            RefreshPlayerSlots();
        }

        private void OnError(string error)
        {
            SetStatusText($"Erro: {error}");
        }

        private void UpdateLobbyList(System.Collections.Generic.List<LobbyInfo> lobbies)
        {
            if (lobbyListContent == null || lobbyItemPrefab == null) return;

            foreach (Transform child in lobbyListContent)
                Destroy(child.gameObject);

            foreach (var info in lobbies)
            {
                var item = Instantiate(lobbyItemPrefab, lobbyListContent);
                var itemUI = item.GetComponent<LobbyItemUI>();
                if (itemUI != null)
                    itemUI.Setup(info);
            }
        }

        private void UpdateLobbyRoomUI(LobbyInfo lobby)
        {
            if (lobbyNameText != null)
                lobbyNameText.text = lobby.lobbyName;

            RefreshPlayerSlots();

            if (startGameButton != null)
            {
                var currentLobby = LobbyManager.Instance.GetCurrentLobby();
                string localUid = SessionManager.Instance?.GetUserId() ?? "";
                bool isHost = !string.IsNullOrEmpty(currentLobby?.hostProductUserId) &&
                              currentLobby.hostProductUserId == localUid;
                startGameButton.gameObject.SetActive(isHost);
            }
        }

        private void RefreshPlayerSlots()
        {
            if (playerSlotsContainer == null) return;

            var members = LobbyManager.Instance.GetMembers();
            int maxPlayers = LobbyManager.Instance.GetCurrentLobby()?.maxPlayers ?? 4;

            foreach (Transform child in playerSlotsContainer)
                Destroy(child.gameObject);

            for (int i = 0; i < maxPlayers; i++)
            {
                var slotGO  = new GameObject($"Slot_{i + 1}", typeof(RectTransform));
                slotGO.transform.SetParent(playerSlotsContainer, false);

                var label = slotGO.AddComponent<TMPro.TextMeshProUGUI>();
                if (i < members.Count)
                {
                    var m = members[i];
                    string hostTag   = m.isHost   ? " [Host]" : "";
                    string readyTag  = m.isReady  ? " ✓"      : "";
                    label.text       = $"{m.displayName}{hostTag}{readyTag}";
                    label.color      = m.isReady
                        ? new UnityEngine.Color(0.2f, 0.8f, 0.2f)
                        : UnityEngine.Color.white;
                }
                else
                {
                    label.text  = "— Aguardando —";
                    label.color = new UnityEngine.Color(0.5f, 0.5f, 0.5f);
                }

                label.fontSize  = 18;
                label.alignment = TMPro.TextAlignmentOptions.MidlineLeft;
            }
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
