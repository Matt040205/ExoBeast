using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;
using ExoBeasts.Multiplayer.Auth;
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Multiplayer.Lobby;

namespace ExoBeasts.Multiplayer.Testing
{
    /// <summary>
    /// ── LobbyPlaceholderUI ───────────────────────────────
    /// UI de teste para o sistema de Lobby — sem Canvas, usa OnGUI.
    ///
    ///  ▸ Tres telas: Auth → LobbyList → LobbyRoom
    ///  ▸ Detecta clone MPPM via MppmHelper (command-line args do Unity 6)
    ///  ▸ Host detectado via ProductUserId (nao displayName)
    ///  ▸ Log de eventos com historico dos ultimos MAX_LOG eventos
    ///  ▸ Painel de debug colapsavel com EOS SDK, NGO, UserId e MPPM
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class LobbyPlaceholderUI : MonoBehaviour
    {
        private enum Screen { Auth, LobbyList, LobbyRoom }
        private Screen _screen = Screen.Auth;

        private string _displayName = "Jogador";
        private bool   _isMppmClone = false;
        private string _mppmCloneId = "";
        private bool   _loggingIn   = false;

        private string          _lobbyNameFilter  = "";
        private string          _joinByIdInput    = "";
        private List<LobbyInfo> _foundLobbies     = new List<LobbyInfo>();
        private Vector2         _lobbyListScroll;
        private bool            _showingCreate    = false;

        private string _newLobbyName   = "Minha Sala";
        private int    _newMaxPlayers  = 4;
        private bool   _newIsPublic    = true;

        private bool   _isReady        = false;
        private string _currentLobbyId = "";

        private readonly List<string> _eventLog = new List<string>();
        private const int MAX_LOG = 5;
        private string _status = "";

        private bool _showDebug = false;

        private EOSAuthenticator  _authCache;
        private LobbyManager      _lobbyCache;
        private EOSManagerWrapper _eosCache;
        private bool              _eosReady = false;

        private void Start()
        {
            // Detectar clone MPPM via command-line args (Unity 6 MPPM v1.6+)
            _isMppmClone = MppmHelper.IsClone;
            _mppmCloneId = MppmHelper.CloneId;
            if (_isMppmClone)
            {
                string shortId = _mppmCloneId.Length > 4
                    ? _mppmCloneId.Substring(0, 4)
                    : _mppmCloneId;
                _displayName = $"Clone_{shortId}";
            }

            _eosCache   = EOSManagerWrapper.Instance;
            _authCache  = EOSAuthenticator.Instance;
            _lobbyCache = LobbyManager.Instance;

            _eosReady = _eosCache.IsInitialized;
            _eosCache.OnEOSInitialized       += OnEOSReady;
            _eosCache.OnInitializationFailed += OnEOSFailed;

            if (_eosReady)
                AddLog("EOS SDK pronto.");
            else
                AddLog("Aguardando inicializacao do EOS SDK...");

            if (_authCache.IsLoggedIn)
            {
                _displayName = SessionManager.Instance.GetDisplayName();
                _screen = Screen.LobbyList;
            }

            SubscribeToEvents();
        }

        private void OnEOSReady()
        {
            _eosReady = true;
            AddLog("EOS SDK pronto. Faca login para continuar.");
        }

        private void OnEOSFailed(string error)
        {
            AddLog($"Falha EOS: {error}");
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        private void SubscribeToEvents()
        {
            _authCache.OnLoginSuccess += OnLoginSuccess;
            _authCache.OnLoginFailed  += OnLoginFailed;

            _lobbyCache.OnLobbyCreated  += OnLobbyCreated;
            _lobbyCache.OnLobbiesFound  += OnLobbiesFound;
            _lobbyCache.OnLobbyJoined   += OnLobbyJoined;
            _lobbyCache.OnLobbyLeft     += OnLobbyLeft;
            _lobbyCache.OnMemberJoined  += OnMemberJoined;
            _lobbyCache.OnMemberLeft    += OnMemberLeft;
            _lobbyCache.OnMemberUpdated += OnMemberUpdated;
            _lobbyCache.OnError         += OnError;
        }

        private void UnsubscribeFromEvents()
        {
            if (_eosCache != null)
            {
                _eosCache.OnEOSInitialized       -= OnEOSReady;
                _eosCache.OnInitializationFailed -= OnEOSFailed;
            }

            if (_authCache != null)
            {
                _authCache.OnLoginSuccess -= OnLoginSuccess;
                _authCache.OnLoginFailed  -= OnLoginFailed;
            }

            if (_lobbyCache != null)
            {
                _lobbyCache.OnLobbyCreated  -= OnLobbyCreated;
                _lobbyCache.OnLobbiesFound  -= OnLobbiesFound;
                _lobbyCache.OnLobbyJoined   -= OnLobbyJoined;
                _lobbyCache.OnLobbyLeft     -= OnLobbyLeft;
                _lobbyCache.OnMemberJoined  -= OnMemberJoined;
                _lobbyCache.OnMemberLeft    -= OnMemberLeft;
                _lobbyCache.OnMemberUpdated -= OnMemberUpdated;
                _lobbyCache.OnError         -= OnError;
            }
        }

        private void AddLog(string msg)
        {
            _eventLog.Add(msg);
            if (_eventLog.Count > MAX_LOG)
                _eventLog.RemoveAt(0);
            _status = msg;
        }

        private void OnLoginSuccess(string userId)
        {
            _loggingIn   = false;
            _displayName = SessionManager.Instance.GetDisplayName();
            AddLog($"Logado como '{_displayName}'");
            _screen = Screen.LobbyList;
        }

        private void OnLoginFailed(string error)
        {
            _loggingIn = false;
            AddLog($"Falha no login: {error}");
        }

        private void OnLobbyCreated(LobbyInfo lobby)
        {
            _currentLobbyId = lobby.lobbyId;
            AddLog($"Sala criada: {lobby.lobbyName}");
            _isReady = false;
            _screen  = Screen.LobbyRoom;
        }

        private void OnLobbiesFound(List<LobbyInfo> lobbies)
        {
            _foundLobbies = lobbies;
            AddLog(lobbies.Count > 0
                ? $"{lobbies.Count} lobby(s) encontrado(s)"
                : "Nenhum lobby encontrado");
        }

        private void OnLobbyJoined(LobbyInfo lobby)
        {
            _currentLobbyId = lobby.lobbyId;
            AddLog($"Entrou em: {lobby.lobbyName}");
            _isReady = false;
            _screen  = Screen.LobbyRoom;
        }

        private void OnLobbyLeft()
        {
            _currentLobbyId = "";
            _isReady        = false;
            AddLog("Saiu do lobby");
            _screen = Screen.LobbyList;
        }

        private void OnMemberJoined(LobbyMember member)
            => AddLog($">> {member.displayName} entrou na sala");

        private void OnMemberLeft(LobbyMember member)
            => AddLog($"<< {member.displayName} saiu da sala");

        private void OnMemberUpdated(LobbyMember member)
            => AddLog($"[{member.displayName}] {(member.isReady ? "esta pronto ✓" : "nao esta pronto")}");

        private void OnError(string error)
            => AddLog($"Erro: {error}");

        private void OnGUI()
        {
            GUILayout.BeginArea(new Rect(10, 10, 520, 720));
            GUILayout.BeginVertical("box");

            GUILayout.BeginHorizontal();
            GUILayout.Label("=== ExoBeasts — Lobby [PLACEHOLDER] ===");
            GUILayout.FlexibleSpace();
            var debugStyle = new GUIStyle(GUI.skin.button);
            debugStyle.normal.textColor = _showDebug ? Color.yellow : Color.white;
            if (GUILayout.Button("[DEBUG]", debugStyle, GUILayout.Width(70)))
                _showDebug = !_showDebug;
            GUILayout.EndHorizontal();

            if (_showDebug)
                DrawDebugPanel();

            GUILayout.Space(4);

            int logLines = (_screen == Screen.LobbyRoom) ? MAX_LOG : 2;
            if (_eventLog.Count > 0)
            {
                int start = Mathf.Max(0, _eventLog.Count - logLines);
                for (int i = start; i < _eventLog.Count; i++)
                {
                    string entry = _eventLog[i];
                    var style = new GUIStyle(GUI.skin.label);
                    style.normal.textColor = entry.StartsWith("Erro") ? Color.red :
                                             entry.StartsWith(">>")   ? Color.green :
                                             entry.StartsWith("<<")   ? new Color(1f, 0.6f, 0.2f) :
                                                                         Color.cyan;
                    GUILayout.Label(entry, style);
                }
            }

            GUILayout.Space(6);

            switch (_screen)
            {
                case Screen.Auth:      DrawAuthScreen();      break;
                case Screen.LobbyList: DrawLobbyListScreen(); break;
                case Screen.LobbyRoom: DrawLobbyRoomScreen(); break;
            }

            GUILayout.Space(8);
            GUILayout.Label("Console para logs detalhados.");
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawDebugPanel()
        {
            GUILayout.BeginVertical("box");
            var titleStyle = new GUIStyle(GUI.skin.label);
            titleStyle.normal.textColor = Color.yellow;
            GUILayout.Label("─── Debug EOS / NGO ───", titleStyle);

            GUILayout.Label($"EOS SDK: {(_eosReady ? "OK" : "NAO PRONTO")}");

            if (_isMppmClone)
            {
                var mppmStyle = new GUIStyle(GUI.skin.label);
                mppmStyle.normal.textColor = Color.cyan;
                GUILayout.Label($"MPPM Clone: {_mppmCloneId}", mppmStyle);
            }

            if (_authCache != null)
                GUILayout.Label($"Auth: {(_authCache.IsLoggedIn ? "Logado" : "Deslogado")}");

            string uid = SessionManager.Instance?.GetUserId() ?? "";
            if (!string.IsNullOrEmpty(uid))
            {
                string shortUid = uid.Length > 16 ? uid.Substring(0, 16) + "..." : uid;
                GUILayout.Label($"UserId: {shortUid}");
            }
            else
            {
                GUILayout.Label("UserId: (nenhum)");
            }

            if (!string.IsNullOrEmpty(_currentLobbyId))
                GUILayout.Label($"Lobby ID: {_currentLobbyId}");
            else
                GUILayout.Label("Lobby ID: (fora de lobby)");

            if (_lobbyCache != null)
                GUILayout.Label($"Membros (local): {_lobbyCache.GetMembers().Count}");

            string ngoState = "N/A";
            if (NetworkManager.Singleton != null)
            {
                if (NetworkManager.Singleton.IsHost)         ngoState = "Host";
                else if (NetworkManager.Singleton.IsClient)  ngoState = "Client";
                else                                          ngoState = "Desconectado";
            }
            GUILayout.Label($"NGO: {ngoState}");

            GUILayout.EndVertical();
        }

        private void DrawAuthScreen()
        {
            GUILayout.Label("─── Autenticacao EOS ───");
            GUILayout.Space(6);

            GUILayout.Label("Nome de exibicao:");
            _displayName = GUILayout.TextField(_displayName, GUILayout.Width(260));

            GUILayout.Space(8);

            GUI.enabled = _eosReady && !_loggingIn;
            string btnLabel = !_eosReady    ? "Aguardando EOS SDK..." :
                               _loggingIn   ? "Conectando..." :
                                              "Login via Device ID (anonimo)";
            if (GUILayout.Button(btnLabel, GUILayout.Height(42)))
            {
                _loggingIn = true;
                AddLog("Conectando ao EOS...");
                _authCache.SetDeviceIdName(_displayName);
                _authCache.LoginWithDeviceId();
            }
            GUI.enabled = true;

            GUILayout.Space(6);
            GUILayout.Label("(Device ID = login anonimo, sem conta Epic necessaria)");
        }

        private void DrawLobbyListScreen()
        {
            GUILayout.Label($"─── Lobbies Disponiveis | Logado: {_displayName} ───");
            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Buscar:", GUILayout.Width(52));
            _lobbyNameFilter = GUILayout.TextField(_lobbyNameFilter, GUILayout.Width(200));
            if (GUILayout.Button("Buscar", GUILayout.Width(70)))
            {
                AddLog("Buscando...");
                _lobbyCache.SearchLobbies(new LobbySearchFilter { lobbyName = _lobbyNameFilter });
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);

            if (!_showingCreate)
            {
                if (_foundLobbies.Count == 0)
                {
                    GUILayout.Label("(nenhum lobby encontrado — clique Buscar)");
                }
                else
                {
                    _lobbyListScroll = GUILayout.BeginScrollView(_lobbyListScroll,
                        GUILayout.Height(Mathf.Min(_foundLobbies.Count * 38 + 10, 200)));

                    foreach (var lobby in _foundLobbies)
                    {
                        GUILayout.BeginHorizontal("box");
                        GUILayout.Label($"{lobby.lobbyName}  [{lobby.currentPlayers}/{lobby.maxPlayers}]");
                        GUILayout.FlexibleSpace();
                        bool isFull = lobby.currentPlayers >= lobby.maxPlayers;
                        GUI.enabled = !isFull;
                        if (GUILayout.Button(isFull ? "Cheio" : "Entrar", GUILayout.Width(70)))
                        {
                            AddLog($"Entrando em '{lobby.lobbyName}'...");
                            _lobbyCache.JoinLobby(lobby.lobbyId);
                        }
                        GUI.enabled = true;
                        GUILayout.EndHorizontal();
                    }

                    GUILayout.EndScrollView();
                }

                GUILayout.Space(6);

                GUILayout.BeginHorizontal();
                GUILayout.Label("ID:", GUILayout.Width(24));
                _joinByIdInput = GUILayout.TextField(_joinByIdInput, GUILayout.Width(268));
                if (GUILayout.Button("Entrar", GUILayout.Width(70)) &&
                    !string.IsNullOrWhiteSpace(_joinByIdInput))
                {
                    AddLog($"Buscando lobby '{_joinByIdInput}'...");
                    _lobbyCache.JoinLobby(_joinByIdInput.Trim());
                }
                GUILayout.EndHorizontal();

                GUILayout.Space(6);
                if (GUILayout.Button("+ Criar Novo Lobby", GUILayout.Height(38)))
                    _showingCreate = true;
            }
            else
            {
                DrawCreateLobbySubPanel();
            }
        }

        private void DrawCreateLobbySubPanel()
        {
            GUILayout.Label("─── Novo Lobby ───");
            GUILayout.Space(4);

            GUILayout.Label("Nome da sala:");
            _newLobbyName = GUILayout.TextField(_newLobbyName, GUILayout.Width(260));

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Max jogadores:", GUILayout.Width(110));
            if (GUILayout.Button("−", GUILayout.Width(28)) && _newMaxPlayers > 2)
                _newMaxPlayers--;
            GUILayout.Label(_newMaxPlayers.ToString(), GUILayout.Width(24));
            if (GUILayout.Button("+", GUILayout.Width(28)) && _newMaxPlayers < 4)
                _newMaxPlayers++;
            GUILayout.EndHorizontal();

            GUILayout.Space(4);

            GUILayout.BeginHorizontal();
            GUILayout.Label("Publico:", GUILayout.Width(58));
            _newIsPublic = GUILayout.Toggle(_newIsPublic, _newIsPublic ? "Sim" : "Nao");
            GUILayout.EndHorizontal();

            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Criar", GUILayout.Height(38), GUILayout.Width(120)))
            {
                AddLog("Criando lobby...");
                _lobbyCache.CreateLobby(new LobbySettings
                {
                    lobbyName  = string.IsNullOrWhiteSpace(_newLobbyName) ? "Minha Sala" : _newLobbyName,
                    maxPlayers = _newMaxPlayers,
                    isPublic   = _newIsPublic,
                    mapName    = "SceneMapTest",
                });
                _showingCreate = false;
            }
            GUILayout.Space(8);
            if (GUILayout.Button("Cancelar", GUILayout.Height(38), GUILayout.Width(100)))
                _showingCreate = false;
            GUILayout.EndHorizontal();
        }

        private void DrawLobbyRoomScreen()
        {
            var lobby   = _lobbyCache.GetCurrentLobby();
            var members = _lobbyCache.GetMembers();

            string lobbyName = lobby?.lobbyName ?? "Sala";
            GUILayout.Label($"─── Sala: {lobbyName} ───");

            var countStyle = new GUIStyle(GUI.skin.label);
            countStyle.normal.textColor = Color.cyan;
            GUILayout.Label($"Jogadores: {members.Count}/{(lobby?.maxPlayers ?? 4)}", countStyle);

            if (lobby != null && !string.IsNullOrEmpty(lobby.lobbyId))
            {
                GUILayout.BeginHorizontal();
                string shortId = lobby.lobbyId.Length > 20
                    ? lobby.lobbyId.Substring(0, 20) + "..."
                    : lobby.lobbyId;
                GUILayout.Label($"ID: {shortId}", GUILayout.ExpandWidth(true));
                if (GUILayout.Button("Copiar ID", GUILayout.Width(80)))
                {
                    GUIUtility.systemCopyBuffer = lobby.lobbyId;
                    AddLog("ID copiado para area de transferencia!");
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(6);
            GUILayout.Label("Jogadores:");

            int max = lobby?.maxPlayers ?? 4;
            for (int i = 0; i < max; i++)
            {
                var style = new GUIStyle(GUI.skin.label);
                string line;
                if (i < members.Count)
                {
                    var m = members[i];
                    string localUid = SessionManager.Instance?.GetUserId() ?? "";
                    bool isMe = !string.IsNullOrEmpty(localUid) && m.productUserId == localUid;

                    string tags = (m.isHost ? " [Host]" : "") +
                                  (m.isReady ? " ✓" : "") +
                                  (isMe ? " ◄ VOCE" : "");
                    line = $"  {i + 1}. {m.displayName}{tags}";
                    style.normal.textColor = isMe     ? Color.yellow :
                                             m.isReady ? Color.green  : Color.white;
                }
                else
                {
                    line = $"  {i + 1}. — Aguardando —";
                    style.normal.textColor = Color.gray;
                }
                GUILayout.Label(line, style);
            }

            GUILayout.Space(10);

            bool newReady = GUILayout.Toggle(_isReady, "  Estou Pronto");
            if (newReady != _isReady)
            {
                _isReady = newReady;
                _lobbyCache.SetReady(_isReady);
            }

            GUILayout.Space(8);

            bool isHost = !string.IsNullOrEmpty(lobby?.hostProductUserId) &&
                          lobby.hostProductUserId == SessionManager.Instance?.GetUserId();
            if (isHost)
            {
                if (GUILayout.Button("Iniciar Partida", GUILayout.Height(42)))
                {
                    AddLog("Iniciando partida...");
                    _lobbyCache.StartMatch();
                }
                GUILayout.Space(4);
            }

            if (GUILayout.Button("Sair da Sala", GUILayout.Height(34)))
            {
                AddLog("Saindo...");
                _lobbyCache.LeaveLobby();
            }
        }
    }
}
