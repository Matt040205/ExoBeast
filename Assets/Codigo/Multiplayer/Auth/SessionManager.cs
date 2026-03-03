using UnityEngine;

namespace ExoBeasts.Multiplayer.Auth
{
    /// <summary>
    /// Gerencia a sessao do usuario
    /// Armazena informacoes do jogador logado e estado da sessao
    /// </summary>
    public class SessionManager : MonoBehaviour
    {
        private static SessionManager _instance;
        public static SessionManager Instance
        {
            get
            {
                if (_instance == null)
                {
                    GameObject go = new GameObject("SessionManager");
                    _instance = go.AddComponent<SessionManager>();
                }
                return _instance;
            }
        }

        [Header("Session Data")]
        private string userId = "";
        private string displayName = "";
        private bool isInSession = false;

        // Dados da sessao atual
        private string currentLobbyId = "";
        private string currentMatchId = "";

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);
        }

        /// <summary>
        /// Iniciar uma nova sessao de usuario
        /// </summary>
        public void StartSession(string userId, string displayName)
        {
            this.userId = userId;
            this.displayName = displayName;
            isInSession = true;

            Debug.Log($"[SessionManager] Sessao iniciada para: {displayName} (ID: {userId})");
        }

        /// <summary>
        /// Encerrar a sessao atual
        /// </summary>
        public void EndSession()
        {
            Debug.Log($"[SessionManager] Encerrando sessao de: {displayName}");

            userId = "";
            displayName = "";
            currentLobbyId = "";
            currentMatchId = "";
            isInSession = false;
        }

        /// <summary>
        /// Definir o lobby atual
        /// </summary>
        public void SetCurrentLobby(string lobbyId)
        {
            currentLobbyId = lobbyId;
            Debug.Log($"[SessionManager] Lobby atual: {lobbyId}");
        }

        /// <summary>
        /// Definir a partida atual
        /// </summary>
        public void SetCurrentMatch(string matchId)
        {
            currentMatchId = matchId;
            Debug.Log($"[SessionManager] Partida atual: {matchId}");
        }

        // Getters
        public string GetUserId() => userId;
        public string GetDisplayName() => displayName;
        public bool IsInSession() => isInSession;
        public string GetCurrentLobbyId() => currentLobbyId;
        public string GetCurrentMatchId() => currentMatchId;

        /// <summary>
        /// Verifica se o jogador esta em um lobby
        /// </summary>
        public bool IsInLobby()
        {
            return !string.IsNullOrEmpty(currentLobbyId);
        }

        /// <summary>
        /// Verifica se o jogador esta em uma partida
        /// </summary>
        public bool IsInMatch()
        {
            return !string.IsNullOrEmpty(currentMatchId);
        }
    }
}
