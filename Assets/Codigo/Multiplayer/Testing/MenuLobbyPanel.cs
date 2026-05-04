using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using ExoBeasts.Multiplayer.Auth;
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Multiplayer.Lobby;

namespace ExoBeasts.Multiplayer.Testing
{
    /// <summary>
    /// ── MenuLobbyPanel ───────────────────────────────────
    /// Painel de lobby simples para teste na MenuScene — sem Canvas, usa OnGUI.
    ///
    ///  ▸ Dois estados: Idle (criar/entrar) → InLobby (sala com jogadores)
    ///  ▸ Ativado via Mostrar() / Fechar() — ideal para onClick de botao
    ///  ▸ Detecta host via hostProductUserId (igual ao LobbyPlaceholderUI)
    ///  ▸ Copiar ID da sala para clipboard com um clique
    ///  ▸ Nao tem tela de auth — exibe aviso se nao estiver logado
    /// ─────────────────────────────────────────────────────
    /// </summary>
    public class MenuLobbyPanel : MonoBehaviour
    {
        private enum PanelState { Idle, InLobby, CharacterSelect }
        private PanelState _state = PanelState.Idle;

        private bool _visible = false;

        // --- Idle ---
        private string _newLobbyName  = "Minha Sala";
        private string _joinByIdInput = "";
        private bool   _showCreate    = false;
        private int    _newMaxPlayers = 4;

        // --- InLobby ---
        private string _currentLobbyId   = "";
        private string _currentLobbyName = "";
        private bool   _isReady          = false;

        // --- Log ---
        private readonly List<string> _log = new List<string>();
        private const int MAX_LOG = 4;

        // --- Seleção de Personagem ---
        private int[]  _mySlotChoices      = System.Array.Empty<int>();
        private bool   _selectionConfirmed = false;
        private bool   _showSlotPicker     = false;
        private int    _pickerSlotIndex    = -1;
        private static readonly string[] _charNames = { "Coruja", "Samurai" };

        // --- Singletons ---
        private EOSAuthenticator _auth;
        private LobbyManager     _lobby;

        // ──────────────────────────────────────────────────────────────────────
        // Ciclo de vida
        // ──────────────────────────────────────────────────────────────────────

        private void Start()
        {
            _auth  = EOSAuthenticator.Instance;
            _lobby = LobbyManager.Instance;
            SubscribeToEvents();
            StartCoroutine(AutoLoginWhenReady());
        }

        private System.Collections.IEnumerator AutoLoginWhenReady()
        {
            // Aguarda EOSManagerWrapper inicializar (pode ser assíncrono na primeira vez)
            float elapsed = 0f;
            while (!EOSManagerWrapper.Instance.IsInitialized && elapsed < 15f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (_auth == null) _auth = EOSAuthenticator.Instance;
            if (_auth == null || _auth.IsLoggedIn) yield break;

            string nick = PlayerPrefs.GetString("PlayerDisplayName", "");
            if (string.IsNullOrWhiteSpace(nick))
            {
                nick = "Jogador_" + UnityEngine.Random.Range(1000, 9999);
                PlayerPrefs.SetString("PlayerDisplayName", nick);
                PlayerPrefs.Save();
            }

            _auth.SetDeviceIdName(nick);
            _auth.LoginWithDeviceId();
        }

        private void OnDestroy()
        {
            UnsubscribeFromEvents();
        }

        // ──────────────────────────────────────────────────────────────────────
        // API publica (para botoes OnClick no Inspector)
        // ──────────────────────────────────────────────────────────────────────

        public void Mostrar()
        {
            // Re-cacheia caso os singletons tenham sido criados depois do Start
            if (_auth  == null) _auth  = EOSAuthenticator.Instance;
            if (_lobby == null) _lobby = LobbyManager.Instance;

            // Se ja estiver numa sala, abre direto no estado correto
            if (_lobby != null && _lobby.IsInLobby())
            {
                var lobby = _lobby.GetCurrentLobby();
                if (lobby != null)
                {
                    _currentLobbyId   = lobby.lobbyId;
                    _currentLobbyName = lobby.lobbyName;
                }
                _state = PanelState.InLobby;
            }
            else
            {
                _state = PanelState.Idle;
            }

            _visible = true;
        }

        public void Fechar()
        {
            _visible = false;
            // Nao sai da sala automaticamente — o jogador pode fechar por acidente
        }

        // ──────────────────────────────────────────────────────────────────────
        // Eventos do LobbyManager
        // ──────────────────────────────────────────────────────────────────────

        private void SubscribeToEvents()
        {
            if (_lobby == null) return;
            _lobby.OnLobbyCreated  += OnLobbyCreated;
            _lobby.OnLobbyJoined   += OnLobbyJoined;
            _lobby.OnLobbyLeft     += OnLobbyLeft;
            _lobby.OnMemberJoined  += OnMemberJoined;
            _lobby.OnMemberLeft    += OnMemberLeft;
            _lobby.OnMemberUpdated += OnMemberUpdated;
            _lobby.OnError         += OnError;
        }

        private void UnsubscribeFromEvents()
        {
            if (_lobby == null) return;
            _lobby.OnLobbyCreated  -= OnLobbyCreated;
            _lobby.OnLobbyJoined   -= OnLobbyJoined;
            _lobby.OnLobbyLeft     -= OnLobbyLeft;
            _lobby.OnMemberJoined  -= OnMemberJoined;
            _lobby.OnMemberLeft    -= OnMemberLeft;
            _lobby.OnMemberUpdated -= OnMemberUpdated;
            _lobby.OnError         -= OnError;
        }

        private void OnLobbyCreated(LobbyInfo lobby)
        {
            _currentLobbyId   = lobby.lobbyId;
            _currentLobbyName = lobby.lobbyName;
            _isReady          = false;
            _state            = PanelState.InLobby;
            AddLog($"Sala criada: {lobby.lobbyName}");
        }

        private void OnLobbyJoined(LobbyInfo lobby)
        {
            _currentLobbyId   = lobby.lobbyId;
            _currentLobbyName = lobby.lobbyName;
            _isReady          = false;
            _state            = PanelState.InLobby;
            AddLog($"Entrou em: {lobby.lobbyName}");
        }

        private void OnLobbyLeft()
        {
            _currentLobbyId   = "";
            _currentLobbyName = "";
            _isReady          = false;
            _state            = PanelState.Idle;
            AddLog("Saiu da sala");
        }

        private void OnMemberJoined(LobbyMember member)
            => AddLog($">> {member.displayName} entrou na sala");

        private void OnMemberLeft(LobbyMember member)
            => AddLog($"<< {member.displayName} saiu da sala");

        private void OnMemberUpdated(LobbyMember member)
            => AddLog($"[{member.displayName}] {(member.isReady ? "pronto ✓" : "nao esta pronto")}");

        private void OnError(string error)
            => AddLog($"[ERRO] {error}");

        private void AddLog(string msg)
        {
            _log.Add(msg);
            if (_log.Count > MAX_LOG)
                _log.RemoveAt(0);
        }

        // ──────────────────────────────────────────────────────────────────────
        // OnGUI
        // ──────────────────────────────────────────────────────────────────────

        private void OnGUI()
        {
            if (!_visible) return;

            float w = 460f;
            float h = _state == PanelState.CharacterSelect ? 680f : 560f;
            float x = (Screen.width  - w) * 0.5f;
            float y = (Screen.height - h) * 0.5f;

            GUILayout.BeginArea(new Rect(x, y, w, h));
            GUILayout.BeginVertical("box");

            DrawHeader();
            DrawLog();
            GUILayout.Space(6);

            switch (_state)
            {
                case PanelState.Idle:            DrawIdlePanel();            break;
                case PanelState.InLobby:         DrawInLobbyPanel();         break;
                case PanelState.CharacterSelect: DrawCharacterSelectPanel(); break;
            }

            GUILayout.Space(6);
            GUILayout.Label("Veja o Console para logs detalhados.");
            GUILayout.EndVertical();
            GUILayout.EndArea();
        }

        private void DrawHeader()
        {
            GUILayout.BeginHorizontal();
            GUILayout.Label("=== ExoBeasts — Lobby ===");
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("[X]", GUILayout.Width(36)))
                Fechar();
            GUILayout.EndHorizontal();
            GUILayout.Space(4);
        }

        private void DrawLog()
        {
            if (_log.Count == 0) return;
            int start = Mathf.Max(0, _log.Count - MAX_LOG);
            for (int i = start; i < _log.Count; i++)
            {
                string entry = _log[i];
                var style = new GUIStyle(GUI.skin.label);
                style.normal.textColor =
                    entry.StartsWith("[ERRO]") ? Color.red     :
                    entry.StartsWith(">>")     ? Color.green   :
                    entry.StartsWith("<<")     ? new Color(1f, 0.6f, 0.2f) :
                                                  Color.cyan;
                GUILayout.Label(entry, style);
            }
        }

        private void DrawIdlePanel()
        {
            bool loggedIn = _auth != null && _auth.IsLoggedIn;

            if (!loggedIn)
            {
                var warnStyle = new GUIStyle(GUI.skin.label);
                warnStyle.normal.textColor = Color.yellow;
                GUILayout.Label("[!] Conectando ao EOS automaticamente...", warnStyle);
                GUILayout.Label("    Aguarde ou verifique a configuracao do EOSManager.", warnStyle);
                GUILayout.Space(6);
            }

            // --- Criar sala ---
            if (!_showCreate)
            {
                GUI.enabled = loggedIn;
                if (GUILayout.Button("+ Criar Sala", GUILayout.Height(38)))
                    _showCreate = true;
                GUI.enabled = true;
            }
            else
            {
                GUILayout.Label("─── Nova Sala ───");
                GUILayout.Space(4);
                GUILayout.Label("Nome da sala:");
                _newLobbyName = GUILayout.TextField(_newLobbyName, GUILayout.Width(280));
                GUILayout.Space(4);

                GUILayout.BeginHorizontal();
                GUILayout.Label("Limite de jogadores:", GUILayout.Width(145));
                if (GUILayout.Button("-", GUILayout.Width(28))) _newMaxPlayers = Mathf.Max(2, _newMaxPlayers - 1);
                GUILayout.Label(_newMaxPlayers.ToString(), GUILayout.Width(24));
                if (GUILayout.Button("+", GUILayout.Width(28))) _newMaxPlayers = Mathf.Min(4, _newMaxPlayers + 1);
                GUILayout.EndHorizontal();
                GUILayout.Space(4);

                GUILayout.BeginHorizontal();
                GUI.enabled = loggedIn;
                if (GUILayout.Button("Criar", GUILayout.Height(36), GUILayout.Width(110)))
                {
                    AddLog("Criando sala...");
                    _lobby.CreateLobby(new LobbySettings
                    {
                        lobbyName  = string.IsNullOrWhiteSpace(_newLobbyName) ? "Minha Sala" : _newLobbyName,
                        maxPlayers = _newMaxPlayers,
                        isPublic   = true,
                        mapName    = "CenaMapaNOVO",
                    });
                    _showCreate = false;
                }
                GUI.enabled = true;
                GUILayout.Space(8);
                if (GUILayout.Button("Cancelar", GUILayout.Height(36), GUILayout.Width(100)))
                    _showCreate = false;
                GUILayout.EndHorizontal();
            }

            GUILayout.Space(10);

            // --- Entrar por ID ---
            GUILayout.Label("─── Entrar por ID ───");
            GUILayout.BeginHorizontal();
            GUILayout.Label("Codigo:", GUILayout.Width(60));
            _joinByIdInput = GUILayout.TextField(_joinByIdInput, GUILayout.Width(240));
            GUI.enabled = loggedIn && !string.IsNullOrWhiteSpace(_joinByIdInput);
            if (GUILayout.Button("Entrar", GUILayout.Width(70)))
            {
                AddLog($"Buscando sala '{_joinByIdInput.Trim()}'...");
                _lobby.JoinLobby(_joinByIdInput.Trim());
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }

        private void DrawInLobbyPanel()
        {
            var lobby   = _lobby.GetCurrentLobby();
            var members = _lobby.GetMembers();

            string lobbyName = lobby?.lobbyName ?? _currentLobbyName;
            GUILayout.Label($"─── Sala: {lobbyName} ───");

            var countStyle = new GUIStyle(GUI.skin.label);
            countStyle.normal.textColor = Color.cyan;
            GUILayout.Label($"Jogadores: {members.Count}/{(lobby?.maxPlayers ?? 4)}", countStyle);

            // --- Codigo da sala com botao Copiar ---
            string idDisplay = _currentLobbyId;
            if (idDisplay.Length > 20) idDisplay = idDisplay.Substring(0, 20) + "...";

            GUILayout.BeginHorizontal();
            GUILayout.Label($"ID: {idDisplay}", GUILayout.ExpandWidth(true));
            if (GUILayout.Button("Copiar ID", GUILayout.Width(80)))
            {
                GUIUtility.systemCopyBuffer = _currentLobbyId;
                AddLog("ID copiado!");
            }
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.Label("Jogadores:");

            // --- Lista de jogadores ---
            int max = lobby?.maxPlayers ?? 4;
            string localUid = SessionManager.Instance?.GetUserId() ?? "";

            for (int i = 0; i < max; i++)
            {
                var style = new GUIStyle(GUI.skin.label);
                string line;

                if (i < members.Count)
                {
                    var m    = members[i];
                    bool isMe   = !string.IsNullOrEmpty(localUid) && m.productUserId == localUid;
                    bool isHost = !string.IsNullOrEmpty(lobby?.hostProductUserId) &&
                                  m.productUserId == lobby.hostProductUserId;

                    string tags = (isHost  ? " [Host]" : "") +
                                  (m.isReady ? " ✓"    : "") +
                                  (isMe     ? " ◄ VOCE" : "");
                    line = $"  {i + 1}. {m.displayName}{tags}";

                    style.normal.textColor = isMe      ? Color.yellow :
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

            // --- Toggle Pronto ---
            bool newReady = GUILayout.Toggle(_isReady, "  Estou Pronto");
            if (newReady != _isReady)
            {
                _isReady = newReady;
                _lobby.SetReady(_isReady);
            }

            GUILayout.Space(8);

            // --- Selecionar Personagens ---
            if (GUILayout.Button("Selecionar Personagens", GUILayout.Height(36)))
                EnterCharacterSelect();

            GUILayout.Space(4);

            // --- Iniciar Partida (apenas host, quando todos prontos) ---
            bool amHost = !string.IsNullOrEmpty(lobby?.hostProductUserId) &&
                          lobby.hostProductUserId == SessionManager.Instance?.GetUserId();
            if (amHost && AllMembersReady())
            {
                if (GUILayout.Button("▶ Iniciar Partida", GUILayout.Height(42)))
                {
                    AddLog("Iniciando partida...");
                    _lobby.StartMatch();
                }
                GUILayout.Space(4);
            }

            // --- Sair ---
            if (GUILayout.Button("Sair da Sala", GUILayout.Height(34)))
            {
                AddLog("Saindo...");
                _lobby.LeaveLobby();
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // Seleção de Personagens
        // ──────────────────────────────────────────────────────────────────────

        private void EnterCharacterSelect()
        {
            int count             = _lobby.GetMembers().Count;
            _mySlotChoices        = new int[SlotsPerPlayer(count)];
            _selectionConfirmed   = false;
            _showSlotPicker       = false;
            _pickerSlotIndex      = -1;
            _state                = PanelState.CharacterSelect;
        }

        // Simplificado para testes: 1 comandante por jogador, sem torres no grid.
        // Layout final (1 comandante + N torres por jogador) sera reintroduzido
        // quando os game designers finalizarem a refatoracao da tela.
        private static int SlotsPerPlayer(int playerCount) => 1;

        private bool AllMembersReady()
        {
            var members = _lobby.GetMembers();
            return members.Count > 0 && members.TrueForAll(m => m.isReady);
        }

        private void DrawCharacterSelectPanel()
        {
            var members        = _lobby.GetMembers();
            int myIndex        = members.FindIndex(m =>
                m.productUserId == SessionManager.Instance?.GetUserId());
            int playerCount    = Mathf.Max(1, members.Count);
            int slotsPerPlayer = _mySlotChoices.Length > 0
                ? _mySlotChoices.Length
                : SlotsPerPlayer(playerCount);

            GUILayout.Label($"─── Seleção de Personagens ({playerCount} jogadores) ───");
            GUILayout.Space(4);

            // ── Grid: linha = tipo de unidade, coluna = jogador ──────────────
            // Simplificado: apenas a linha do Comandante (row 0).
            for (int row = 0; row < slotsPerPlayer; row++)
            {
                GUILayout.Label("  Comandante");
                GUILayout.BeginHorizontal();
                for (int col = 0; col < playerCount; col++)
                    DrawSlotCell(members, col, row, col == myIndex);
                GUILayout.EndHorizontal();
                GUILayout.Space(2);
            }

            // ── Popup de escolha ─────────────────────────────────────────────
            if (_showSlotPicker && _pickerSlotIndex >= 0 &&
                _pickerSlotIndex < _mySlotChoices.Length)
                DrawSlotPicker();

            GUILayout.Space(6);

            // ── Confirmar / Voltar ────────────────────────────────────────────
            GUILayout.BeginHorizontal();
            if (!_selectionConfirmed)
            {
                if (GUILayout.Button("✔ Confirmar Seleção", GUILayout.Height(34)))
                {
                    _lobby.SelectCharacter(_mySlotChoices[0]);
                    _lobby.SetReady(true);
                    _selectionConfirmed = true;
                    _showSlotPicker     = false;
                    AddLog($"Confirmado: Comandante = {_charNames[_mySlotChoices[0]]}");
                }
            }
            else
            {
                var doneStyle = new GUIStyle(GUI.skin.label);
                doneStyle.normal.textColor = Color.green;
                GUILayout.Label("✔ Pronto!", doneStyle, GUILayout.Width(80));

                if (GUILayout.Button("Editar", GUILayout.Width(70), GUILayout.Height(34)))
                {
                    _lobby.SetReady(false);
                    _selectionConfirmed = false;
                }
            }

            GUILayout.FlexibleSpace();
            if (GUILayout.Button("← Voltar", GUILayout.Width(80), GUILayout.Height(34)))
            {
                if (_selectionConfirmed) _lobby.SetReady(false);
                _selectionConfirmed = false;
                _showSlotPicker     = false;
                _state              = PanelState.InLobby;
            }
            GUILayout.EndHorizontal();
        }

        private void DrawSlotCell(List<LobbyMember> members, int col, int row, bool isMe)
        {
            var member = col < members.Count ? members[col] : null;

            var boxStyle = new GUIStyle(GUI.skin.box);
            if (isMe) boxStyle.normal.textColor = Color.yellow;

            GUILayout.BeginVertical(boxStyle, GUILayout.Width(105), GUILayout.Height(54));

            // Nome do jogador
            string playerLabel = member != null
                ? (isMe ? "▶ VOCE" : member.displayName)
                : "—";
            GUILayout.Label(playerLabel, GUILayout.ExpandWidth(true));

            // Escolha atual
            if (isMe && row < _mySlotChoices.Length)
            {
                int choice = _mySlotChoices[row];
                string name = _charNames[choice % _charNames.Length];

                if (!_selectionConfirmed)
                {
                    if (GUILayout.Button(name, GUILayout.Height(22)))
                    {
                        bool sameSlot = _pickerSlotIndex == row && _showSlotPicker;
                        _pickerSlotIndex = row;
                        _showSlotPicker  = !sameSlot;
                    }
                }
                else
                    GUILayout.Label(name);
            }
            else
            {
                // Outro jogador: comandante sincronizado via EOS, torres são "?"
                string display = (row == 0 && member != null && member.selectedCharacterIndex >= 0)
                    ? _charNames[member.selectedCharacterIndex % _charNames.Length]
                    : "?";
                GUILayout.Label(display);
            }

            GUILayout.EndVertical();
            GUILayout.Space(2);
        }

        private void DrawSlotPicker()
        {
            GUILayout.BeginVertical("box");
            GUILayout.Label("Escolher Comandante:");
            GUILayout.BeginHorizontal();
            for (int i = 0; i < _charNames.Length; i++)
            {
                bool current = _mySlotChoices[_pickerSlotIndex] == i;
                GUI.color = current ? Color.yellow : Color.white;
                if (GUILayout.Button(_charNames[i], GUILayout.Width(90)))
                {
                    _mySlotChoices[_pickerSlotIndex] = i;
                    _showSlotPicker = false;
                }
                GUI.color = Color.white;
            }
            GUILayout.EndHorizontal();
            GUILayout.EndVertical();
        }
    }
}
