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

        private enum AuthState { InitializingEOS, WaitingForName, Connecting, Error }
        private AuthState _authState = AuthState.InitializingEOS;

        private string _displayName = "Jogador";
        private bool   _isMppmClone = false;
        private string _mppmCloneId = "";

        private string          _lobbyNameFilter  = "";
        private string          _joinByIdInput    = "";
        private List<LobbyInfo> _foundLobbies     = new List<LobbyInfo>();
        private Vector2         _lobbyListScroll;
        private bool            _showingCreate    = false;

        private string _newLobbyName   = "Minha Sala";
        private int    _newMaxPlayers  = 4;
        private bool   _newIsPublic    = true;

        private bool   _showingNickChange = false;
        private string _pendingNick       = "";

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

            // Carregar nome salvo (MPPM clones usam nome automatico, ignorando PlayerPrefs)
            if (_isMppmClone)
            {
                string shortId = _mppmCloneId.Length > 4
                    ? _mppmCloneId.Substring(0, 4)
                    : _mppmCloneId;
                _displayName = $"Clone_{shortId}";
            }
            else
            {
                string saved = PlayerPrefs.GetString("PlayerDisplayName", "");
                if (!string.IsNullOrWhiteSpace(saved))
                    _displayName = saved;
            }

            _eosCache   = EOSManagerWrapper.Instance;
            _authCache  = EOSAuthenticator.Instance;
            _lobbyCache = LobbyManager.Instance;

            _eosReady = _eosCache.IsInitialized;
            _eosCache.OnEOSInitialized       += OnEOSReady;
            _eosCache.OnInitializationFailed += OnEOSFailed;

            // Se ja esta logado (reentrada na cena), ir direto para lobby list
            if (_authCache.IsLoggedIn)
            {
                _displayName = SessionManager.Instance.GetDisplayName();
                _screen = Screen.LobbyList;
                SubscribeToEvents();
                return;
            }

            // Se EOS ja esta pronto, decidir proximo passo imediatamente
            if (_eosReady)
            {
                AddLog("EOS SDK pronto.");
                DecideAuthState();
            }
            else
            {
                _authState = AuthState.InitializingEOS;
                AddLog("Inicializando EOS SDK...");
            }

            SubscribeToEvents();
        }

        private void OnEOSReady()
        {
            _eosReady = true;
            AddLog("EOS SDK pronto.");
            if (!_authCache.IsLoggedIn)
                DecideAuthState();
        }

        private void OnEOSFailed(string error)
        {
            _authState = AuthState.Error;
            AddLog($"Falha EOS: {error}");
        }

        private void DecideAuthState()
        {
            bool hasName = !string.IsNullOrWhiteSpace(_displayName) && _displayName != "Jogador";

            if (!hasName)
            {
                // Gera nick automatico na primeira execucao — sem tela de login
                _displayName = "Jogador_" + UnityEngine.Random.Range(1000, 9999);
                PlayerPrefs.SetString("PlayerDisplayName", _displayName);
                PlayerPrefs.Save();
                AddLog($"Nick gerado automaticamente: '{_displayName}'");
            }

            _authState = AuthState.Connecting;
            AddLog($"Conectando como '{_displayName}'...");
            _authCache.SetDeviceIdName(_displayName);
            _authCache.LoginWithDeviceId();
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
            _authState   = AuthState.Connecting; // mantém estado até mudar de tela
            _displayName = SessionManager.Instance.GetDisplayName();
            AddLog($"Logado como '{_displayName}'");
            _screen = Screen.LobbyList;
        }

        private void OnLoginFailed(string error)
        {
            _authState = AuthState.Error;
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

            switch (_authState)
            {
                case AuthState.InitializingEOS:
                {
                    string dots = (Time.time % 1f > 0.5f) ? ".." : ".";
                    GUILayout.Label($"Inicializando EOS SDK{dots}");
                    break;
                }

                case AuthState.Connecting:
                {
                    GUILayout.Label("Conectando...");
                    break;
                }

                case AuthState.WaitingForName:
                {
                    GUILayout.Label("Bem-vindo! Como quer ser chamado?");
                    GUILayout.Space(6);
                    _displayName = GUILayout.TextField(_displayName, GUILayout.Width(260));
                    GUILayout.Space(8);
                    if (GUILayout.Button("Jogar", GUILayout.Height(42)))
                    {
                        string name = _displayName.Trim();
                        if (string.IsNullOrEmpty(name)) name = "Jogador";
                        _displayName = name;
                        if (!_isMppmClone)
                        {
                            PlayerPrefs.SetString("PlayerDisplayName", _displayName);
                            PlayerPrefs.Save();
                        }
                        _authState = AuthState.Connecting;
                        AddLog($"Conectando como '{_displayName}'...");
                        _authCache.SetDeviceIdName(_displayName);
                        _authCache.LoginWithDeviceId();
                    }
                    GUILayout.Space(6);
                    GUILayout.Label("(Device ID = login anonimo, sem conta Epic necessaria)");
                    break;
                }

                case AuthState.Error:
                {
                    var errStyle = new GUIStyle(GUI.skin.label);
                    errStyle.normal.textColor = Color.red;
                    GUILayout.Label("Falha na conexao.", errStyle);
                    GUILayout.Space(6);
                    if (GUILayout.Button("Tentar novamente", GUILayout.Height(38)))
                    {
                        if (_eosReady)
                        {
                            DecideAuthState();
                        }
                        else
                        {
                            _authState = AuthState.InitializingEOS;
                            AddLog("Aguardando EOS SDK...");
                        }
                    }
                    break;
                }
            }
        }

        private void DrawLobbyListScreen()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label($"─── Lobbies Disponiveis | Logado: {_displayName} ───");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Alterar Nick", GUILayout.Width(90)))
            {
                _pendingNick       = _displayName;
                _showingNickChange = !_showingNickChange;
                _showingCreate     = false;
            }
            GUILayout.EndHorizontal();

            if (_showingNickChange)
            {
                DrawNickChangeSubPanel();
                return;
            }

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
                    mapName    = "CenaMapaTeste",
                });
                _showingCreate = false;
            }
            GUILayout.Space(8);
            if (GUILayout.Button("Cancelar", GUILayout.Height(38), GUILayout.Width(100)))
                _showingCreate = false;
            GUILayout.EndHorizontal();
        }

        private void DrawNickChangeSubPanel()
        {
            GUILayout.Label("─── Alterar Nick ───");
            GUILayout.Space(6);

            GUILayout.Label("Novo nome:");
            _pendingNick = GUILayout.TextField(_pendingNick, GUILayout.Width(260));

            GUILayout.Space(8);

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Confirmar", GUILayout.Height(38), GUILayout.Width(120)))
            {
                string name = _pendingNick.Trim();
                if (string.IsNullOrEmpty(name)) name = _displayName;

                _displayName = name;

                if (!_isMppmClone)
                {
                    PlayerPrefs.SetString("PlayerDisplayName", _displayName);
                    PlayerPrefs.Save();
                }

                SessionManager.Instance?.SetDisplayName(_displayName);
                _authCache.SetDeviceIdName(_displayName);

                // Se estiver num lobby, atualizar o atributo de membro imediatamente
                if (!string.IsNullOrEmpty(_currentLobbyId))
                    _lobbyCache.SetMemberAttribute(MemberAttributes.DISPLAY_NAME, _displayName);

                AddLog($"Nick alterado para '{_displayName}'");
                _showingNickChange = false;
            }
            GUILayout.Space(8);
            if (GUILayout.Button("Cancelar", GUILayout.Height(38), GUILayout.Width(100)))
                _showingNickChange = false;
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
