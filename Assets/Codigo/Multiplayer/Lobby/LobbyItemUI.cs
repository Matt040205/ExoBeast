using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace ExoBeasts.Multiplayer.Lobby
{
    /// <summary>
    /// ── LobbyItemUI ──────────────────────────────────────
    /// Item individual na lista de lobbies disponiveis (Canvas UI).
    ///
    ///  ▸ Setup(LobbyInfo): popula nome, host e contagem de jogadores
    ///  ▸ Desabilita botao de Entrar se lobby cheio
    ///  ▸ OnJoinButtonClicked: delega para LobbyManager.JoinLobby
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class LobbyItemUI : MonoBehaviour
    {
        [Header("UI References")]
        [SerializeField] private TMP_Text lobbyNameText;
        [SerializeField] private TMP_Text hostNameText;
        [SerializeField] private TMP_Text playerCountText;
        [SerializeField] private Button joinButton;

        private LobbyInfo lobbyInfo;

        private void Start()
        {
            if (joinButton != null)
            {
                joinButton.onClick.AddListener(OnJoinButtonClicked);
            }
        }

        public void Setup(LobbyInfo info)
        {
            lobbyInfo = info;
            UpdateUI();
        }

        private void UpdateUI()
        {
            if (lobbyInfo == null) return;

            if (lobbyNameText != null)
                lobbyNameText.text = lobbyInfo.lobbyName;

            if (hostNameText != null)
                hostNameText.text = $"Host: {lobbyInfo.hostDisplayName}";

            if (playerCountText != null)
                playerCountText.text = $"{lobbyInfo.currentPlayers}/{lobbyInfo.maxPlayers}";

            if (joinButton != null)
                joinButton.interactable = lobbyInfo.currentPlayers < lobbyInfo.maxPlayers;
        }

        private void OnJoinButtonClicked()
        {
            if (lobbyInfo != null)
            {
                Debug.Log($"[LobbyItemUI] Tentando entrar no lobby: {lobbyInfo.lobbyName}");
                LobbyManager.Instance.JoinLobby(lobbyInfo.lobbyId);
            }
        }
    }
}
