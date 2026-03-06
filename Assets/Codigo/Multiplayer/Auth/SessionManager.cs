using UnityEngine;

namespace ExoBeasts.Multiplayer.Auth
{
    /// <summary>
    /// ── SessionManager ───────────────────────────────────
    /// Armazena estado da sessao do usuario logado entre cenas.
    ///
    ///  ▸ StartSession / EndSession: ciclo de vida da sessao
    ///  ▸ SetCurrentLobby / SetCurrentMatch: rastreia contexto atual
    ///  ▸ Singleton com DontDestroyOnLoad
    /// ─────────────────────────────────────────────────────
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

        public void StartSession(string userId, string displayName)
        {
            this.userId = userId;
            this.displayName = displayName;
            isInSession = true;
            Debug.Log($"[SessionManager] Sessao iniciada para: {displayName} (ID: {userId})");
        }

        public void EndSession()
        {
            Debug.Log($"[SessionManager] Encerrando sessao de: {displayName}");
            userId = "";
            displayName = "";
            currentLobbyId = "";
            currentMatchId = "";
            isInSession = false;
        }

        public void SetCurrentLobby(string lobbyId)
        {
            currentLobbyId = lobbyId;
            Debug.Log($"[SessionManager] Lobby atual: {lobbyId}");
        }

        public void SetCurrentMatch(string matchId)
        {
            currentMatchId = matchId;
            Debug.Log($"[SessionManager] Partida atual: {matchId}");
        }

        public string GetUserId() => userId;
        public string GetDisplayName() => displayName;
        public bool IsInSession() => isInSession;
        public string GetCurrentLobbyId() => currentLobbyId;
        public string GetCurrentMatchId() => currentMatchId;

        public bool IsInLobby()
        {
            return !string.IsNullOrEmpty(currentLobbyId);
        }

        public bool IsInMatch()
        {
            return !string.IsNullOrEmpty(currentMatchId);
        }
    }
}
