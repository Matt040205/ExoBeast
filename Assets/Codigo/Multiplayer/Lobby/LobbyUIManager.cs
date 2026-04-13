using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using DG.Tweening;
using ExoBeasts.Multiplayer.Lobby;
using ExoBeasts.Multiplayer.Auth;
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Managers;

/// <summary>
/// ── LobbyUIManager ───────────────────────────────────
/// Painel de lobby OnGUI para a cena EscolherPersonagem.
/// Aparece no lado direito ao clicar no botão Lobby/Multiplayer.
///
///  ▸ AbrirPainelMultiplayer() / FecharPainelMultiplayer() — chamados por botões
///  ▸ ViewCriar:  criar sala + entrar por ID + buscar salas públicas
///  ▸ ViewBuscar: lista de salas públicas encontradas, entrar por item
///  ▸ ViewSala:   código, estado, lista de jogadores + personagem, ready, start, sair
///  ▸ Auto-auth EOS ao abrir em modo Multiplayer
/// ─────────────────────────────────────────────────────
/// </summary>
public class LobbyUIManager : MonoBehaviour
{
    public static LobbyUIManager Instance;

    [Header("Painéis de Slide (Canvas — opcional)")]
    public RectTransform painelSelecao;
    public RectTransform painelLobby;

    [Header("Posições de Animação")]
    public Vector2 posSelecaoCentro  = Vector2.zero;
    public Vector2 posSelecaoLado    = new Vector2(-400, 0);
    public Vector2 posLobbyEscondido = new Vector2(1200, 0);
    public Vector2 posLobbyVisivel   = new Vector2(450, 0);

    // ── Estado interno ─────────────────────────────────────────────────────
    private bool _visible = false;

    private enum ViewState { Criar, Buscar, Sala }
    private ViewState _view = ViewState.Criar;

    // View Criar
    private string _nomeSala      = "Minha Sala";
    private string _joinIdInput   = "";
    private int    _maxPlayers    = 4;
    private bool   _publico       = true;
    private bool   _showCriar     = false;

    // Nick
    private bool   _showNickEdit = false;
    private string _pendingNick  = "";

    // View Buscar
    private List<LobbyInfo> _lobbyResults = new List<LobbyInfo>();
    private bool            _searching    = false;

    // View Sala
    private string _lobbyId       = "";
    private string _lobbyNome     = "";
    private bool   _isReady       = false;
    private int    _selectedChar  = -1;

    // Auth flow guard
    private bool _eosFlowRunning = false;

    // Log
    private readonly List<string> _log = new List<string>();
    private const int MAX_LOG = 4;

    // Nomes dos personagens (índice = CHARACTER_INDEX)
    // Simplificado para testes: pool de 2 personagens.
    // A ordem DEVE corresponder a GameDataManager.bibliotecaOriginalPersonagens
    // ([0] = Coruja, [1] = Samurai) porque o indice e o que chega ao spawn.
    private static readonly string[] _charNames = { "Coruja", "Samurai" };

    // Singletons
    private LobbyManager     _lobby;
    private EOSAuthenticator _auth;

    // ──────────────────────────────────────────────────────────────────────
    // Ciclo de vida
    // ──────────────────────────────────────────────────────────────────────

    private void Awake() => Instance = this;

    private void Start()
    {
        if (painelLobby != null)
            painelLobby.anchoredPosition = posLobbyEscondido;

        _auth  = EOSAuthenticator.Instance;
        _lobby = LobbyManager.Instance;
        SubscribeToEvents();

        if (GameModeManager.CurrentMode == GameMode.Multiplayer)
            StartCoroutine(InitMultiplayerFlow());
    }

    private void OnDestroy() => UnsubscribeFromEvents();

    // ──────────────────────────────────────────────────────────────────────
    // API pública — chamar por botões OnClick no Inspector
    // ──────────────────────────────────────────────────────────────────────

    public void AbrirPainelMultiplayer()
    {
        if (_auth  == null) _auth  = EOSAuthenticator.Instance;
        if (_lobby == null) _lobby = LobbyManager.Instance;

        if (_lobby != null && _lobby.IsInLobby())
        {
            var l = _lobby.GetCurrentLobby();
            if (l != null) { _lobbyId = l.lobbyId; _lobbyNome = l.lobbyName; }
            _view = ViewState.Sala;
        }
        else
        {
            _view = ViewState.Criar;
        }

        _visible = true;

        // Se EOS não estiver autenticado e nenhum fluxo de auth está rodando, inicia agora
        if (!_eosFlowRunning && (_auth == null || !_auth.IsLoggedIn))
            StartCoroutine(InitMultiplayerFlow(openPanel: false));

        if (painelSelecao != null)
            painelSelecao.DOAnchorPos(posSelecaoLado, 0.5f).SetEase(Ease.OutBack);
        if (painelLobby != null)
            painelLobby.DOAnchorPos(posLobbyVisivel, 0.5f).SetEase(Ease.OutBack);
    }

    public void FecharPainelMultiplayer()
    {
        _visible = false;

        if (painelSelecao != null)
            painelSelecao.DOAnchorPos(posSelecaoCentro, 0.5f).SetEase(Ease.InBack);
        if (painelLobby != null)
            painelLobby.DOAnchorPos(posLobbyEscondido, 0.5f).SetEase(Ease.InBack);
    }

    public void AlterarMaxPlayers(int quantidade)
    {
        _maxPlayers = Mathf.Clamp(_maxPlayers + quantidade, 2, 4);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Eventos do LobbyManager
    // ──────────────────────────────────────────────────────────────────────

    private void SubscribeToEvents()
    {
        if (_lobby == null) return;
        _lobby.OnLobbyCreated  += OnLobbyCreated;
        _lobby.OnLobbiesFound  += OnLobbiesFound;
        _lobby.OnLobbyJoined   += OnLobbyJoined;
        _lobby.OnLobbyLeft     += OnLobbyLeft;
        _lobby.OnMemberJoined  += OnMemberEvento;
        _lobby.OnMemberLeft    += OnMemberEvento;
        _lobby.OnMemberUpdated += OnMemberEvento;
        _lobby.OnError         += OnErro;
    }

    private void UnsubscribeFromEvents()
    {
        if (_lobby == null) return;
        _lobby.OnLobbyCreated  -= OnLobbyCreated;
        _lobby.OnLobbiesFound  -= OnLobbiesFound;
        _lobby.OnLobbyJoined   -= OnLobbyJoined;
        _lobby.OnLobbyLeft     -= OnLobbyLeft;
        _lobby.OnMemberJoined  -= OnMemberEvento;
        _lobby.OnMemberLeft    -= OnMemberEvento;
        _lobby.OnMemberUpdated -= OnMemberEvento;
        _lobby.OnError         -= OnErro;
    }

    private void OnLobbyCreated(LobbyInfo lobby)
    {
        _lobbyId = lobby.lobbyId; _lobbyNome = lobby.lobbyName;
        _isReady = false; _selectedChar = -1;
        _view = ViewState.Sala;
        AddLog($"Sala criada: {lobby.lobbyName}");
    }

    private void OnLobbiesFound(List<LobbyInfo> lobbies)
    {
        _lobbyResults = lobbies;
        _searching    = false;
        AddLog($"{lobbies.Count} sala(s) encontrada(s)");
    }

    private void OnLobbyJoined(LobbyInfo lobby)
    {
        _lobbyId = lobby.lobbyId; _lobbyNome = lobby.lobbyName;
        _isReady = false; _selectedChar = -1;
        _view = ViewState.Sala;
        AddLog($"Entrou em: {lobby.lobbyName}");
    }

    private void OnLobbyLeft()
    {
        _lobbyId = ""; _lobbyNome = "";
        _isReady = false; _selectedChar = -1;
        _lobbyResults.Clear();
        _view = ViewState.Criar;
        AddLog("Saiu da sala");
    }

    private void OnMemberEvento(LobbyMember m)  => AddLog($"Membro: {m.displayName}");
    private void OnErro(string e)               => AddLog($"[ERRO] {e}");

    private void AddLog(string msg)
    {
        _log.Add(msg);
        if (_log.Count > MAX_LOG) _log.RemoveAt(0);
        Debug.Log($"[LobbyUIManager] {msg}");
    }

    // ──────────────────────────────────────────────────────────────────────
    // OnGUI — painel no lado direito da tela
    // ──────────────────────────────────────────────────────────────────────

    private void OnGUI()
    {
        if (!_visible) return;

        float w = 440f;
        float h = 620f;
        float x = Screen.width - w - 10f;
        float y = (Screen.height - h) * 0.5f;

        GUILayout.BeginArea(new Rect(x, y, w, h));
        GUILayout.BeginVertical("box");

        DrawHeader();
        DrawLog();
        GUILayout.Space(6);

        switch (_view)
        {
            case ViewState.Criar:  DrawViewCriar();  break;
            case ViewState.Buscar: DrawViewBuscar(); break;
            case ViewState.Sala:   DrawViewSala();   break;
        }

        GUILayout.Space(4);
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
            FecharPainelMultiplayer();
        GUILayout.EndHorizontal();
        GUILayout.Space(4);
    }

    private void DrawLog()
    {
        if (_log.Count == 0) return;
        int start = Mathf.Max(0, _log.Count - MAX_LOG);
        for (int i = start; i < _log.Count; i++)
        {
            string e = _log[i];
            var s = new GUIStyle(GUI.skin.label);
            s.normal.textColor =
                e.StartsWith("[ERRO]") ? Color.red   :
                e.StartsWith(">>")     ? Color.green  :
                e.StartsWith("<<")     ? new Color(1f, 0.6f, 0.2f) :
                                          Color.cyan;
            GUILayout.Label(e, s);
        }
    }

    // ── View: Criar ────────────────────────────────────────────────────────

    private void DrawViewCriar()
    {
        bool logado = _auth != null && _auth.IsLoggedIn;

        if (!logado)
        {
            var warn = new GUIStyle(GUI.skin.label);
            warn.normal.textColor = Color.yellow;
            GUILayout.Label("[!] Aguardando autenticação EOS...", warn);
            GUILayout.Space(4);
        }

        // --- Nick do jogador ---
        string currentNick = SessionManager.Instance?.GetDisplayName() ?? "";
        if (!_showNickEdit)
        {
            GUILayout.BeginHorizontal();
            var nickStyle = new GUIStyle(GUI.skin.label);
            nickStyle.normal.textColor = Color.yellow;
            GUILayout.Label(string.IsNullOrEmpty(currentNick) ? "Nick: —" : $"Nick: {currentNick}", nickStyle);
            GUILayout.FlexibleSpace();
            if (GUILayout.Button("Alterar Nick", GUILayout.Width(90)))
            {
                _pendingNick  = currentNick;
                _showNickEdit = true;
                _showCriar    = false;
            }
            GUILayout.EndHorizontal();
        }
        else
        {
            GUILayout.Label("─── Alterar Nick ───");
            _pendingNick = GUILayout.TextField(_pendingNick, GUILayout.Width(260));
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("Confirmar", GUILayout.Width(100)))
            {
                string name = _pendingNick.Trim();
                if (!string.IsNullOrEmpty(name))
                {
                    SessionManager.Instance?.SetDisplayName(name);
                    _auth?.SetDeviceIdName(name);
                    PlayerPrefs.SetString("PlayerDisplayName", name);
                    PlayerPrefs.Save();
                    AddLog($"Nick alterado para '{name}'");
                }
                _showNickEdit = false;
            }
            if (GUILayout.Button("Cancelar", GUILayout.Width(80)))
                _showNickEdit = false;
            GUILayout.EndHorizontal();
            GUILayout.Space(6);
            return; // esconde o resto enquanto edita o nick
        }

        GUILayout.Space(6);

        // --- Criar sala ---
        if (!_showCriar)
        {
            GUI.enabled = logado;
            if (GUILayout.Button("+ Criar Sala", GUILayout.Height(38)))
                _showCriar = true;
            GUI.enabled = true;
        }
        else
        {
            GUILayout.Label("─── Nova Sala ───");
            GUILayout.Label("Nome:");
            _nomeSala = GUILayout.TextField(_nomeSala, GUILayout.Width(280));

            GUILayout.BeginHorizontal();
            GUILayout.Label("Max jogadores:", GUILayout.Width(100));
            if (GUILayout.Button("−", GUILayout.Width(28)) && _maxPlayers > 2) _maxPlayers--;
            GUILayout.Label(_maxPlayers.ToString(), GUILayout.Width(20));
            if (GUILayout.Button("+", GUILayout.Width(28)) && _maxPlayers < 4) _maxPlayers++;
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            GUILayout.Label("Público:", GUILayout.Width(58));
            _publico = GUILayout.Toggle(_publico, _publico ? "Sim" : "Não");
            GUILayout.EndHorizontal();

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            GUI.enabled = logado;
            if (GUILayout.Button("Criar", GUILayout.Height(36), GUILayout.Width(110)))
            {
                AddLog("Criando sala...");
                _lobby.CreateLobby(new LobbySettings
                {
                    lobbyName  = string.IsNullOrWhiteSpace(_nomeSala) ? "Minha Sala" : _nomeSala,
                    maxPlayers = _maxPlayers,
                    isPublic   = _publico,
                    mapName    = "CenaMapaTeste",
                });
                _showCriar = false;
            }
            GUI.enabled = true;
            GUILayout.Space(6);
            if (GUILayout.Button("Cancelar", GUILayout.Height(36), GUILayout.Width(100)))
                _showCriar = false;
            GUILayout.EndHorizontal();
        }

        GUILayout.Space(10);

        // --- Entrar por ID ---
        GUILayout.Label("─── Entrar por ID ───");
        GUILayout.BeginHorizontal();
        GUILayout.Label("Código:", GUILayout.Width(55));
        _joinIdInput = GUILayout.TextField(_joinIdInput, GUILayout.Width(230));
        GUI.enabled = logado && !string.IsNullOrWhiteSpace(_joinIdInput);
        if (GUILayout.Button("Entrar", GUILayout.Width(70)))
        {
            AddLog($"Buscando '{_joinIdInput.Trim()}'...");
            _lobby.JoinLobby(_joinIdInput.Trim());
        }
        GUI.enabled = true;
        GUILayout.EndHorizontal();

        GUILayout.Space(10);

        // --- Buscar salas públicas ---
        GUILayout.Label("─── Salas Públicas ───");
        GUI.enabled = logado;
        if (GUILayout.Button("Buscar Salas", GUILayout.Height(34)))
        {
            _searching = true;
            _lobbyResults.Clear();
            _lobby.SearchLobbies(new LobbySearchFilter { onlyPublic = true, maxResults = 10 });
            _view = ViewState.Buscar;
            AddLog("Buscando salas públicas...");
        }
        GUI.enabled = true;
    }

    // ── View: Buscar ───────────────────────────────────────────────────────

    private void DrawViewBuscar()
    {
        GUILayout.BeginHorizontal();
        GUILayout.Label("─── Salas Públicas ───");
        GUILayout.FlexibleSpace();
        if (GUILayout.Button("Voltar", GUILayout.Width(70)))
            _view = ViewState.Criar;
        GUILayout.EndHorizontal();

        GUILayout.Space(4);

        if (GUILayout.Button("Atualizar", GUILayout.Height(30)))
        {
            _searching = true;
            _lobbyResults.Clear();
            _lobby.SearchLobbies(new LobbySearchFilter { onlyPublic = true, maxResults = 10 });
            AddLog("Atualizando...");
        }

        GUILayout.Space(6);

        if (_searching)
        {
            var buscando = new GUIStyle(GUI.skin.label);
            buscando.normal.textColor = Color.yellow;
            GUILayout.Label("Buscando...", buscando);
            return;
        }

        if (_lobbyResults.Count == 0)
        {
            var cinza = new GUIStyle(GUI.skin.label);
            cinza.normal.textColor = Color.gray;
            GUILayout.Label("Nenhuma sala encontrada.", cinza);
            return;
        }

        foreach (var item in _lobbyResults)
        {
            GUILayout.BeginHorizontal("box");
            GUILayout.Label($"{item.lobbyName}", GUILayout.Width(180));
            var cnt = new GUIStyle(GUI.skin.label);
            cnt.normal.textColor = Color.cyan;
            GUILayout.Label($"{item.currentPlayers}/{item.maxPlayers}", cnt, GUILayout.Width(40));
            GUILayout.FlexibleSpace();
            bool cheio = item.currentPlayers >= item.maxPlayers;
            GUI.enabled = !cheio;
            if (GUILayout.Button("Entrar", GUILayout.Width(70)))
            {
                AddLog($"Entrando em '{item.lobbyName}'...");
                _lobby.JoinLobby(item.lobbyId);
            }
            GUI.enabled = true;
            GUILayout.EndHorizontal();
        }
    }

    // ── View: Sala ─────────────────────────────────────────────────────────

    private void DrawViewSala()
    {
        var lobby   = _lobby.GetCurrentLobby();
        var members = _lobby.GetMembers();

        GUILayout.Label($"─── Sala: {_lobbyNome} ───");

        var cyan = new GUIStyle(GUI.skin.label);
        cyan.normal.textColor = Color.cyan;
        GUILayout.Label($"Jogadores: {members.Count}/{(lobby?.maxPlayers ?? 4)}", cyan);
        GUILayout.Label($"Estado: {lobby?.state}", cyan);

        // Código da sala + copiar
        string idCurto = _lobbyId.Length > 20 ? _lobbyId.Substring(0, 20) + "..." : _lobbyId;
        GUILayout.BeginHorizontal();
        GUILayout.Label($"ID: {idCurto}", GUILayout.ExpandWidth(true));
        if (GUILayout.Button("Copiar", GUILayout.Width(70)))
        {
            GUIUtility.systemCopyBuffer = _lobbyId;
            AddLog("ID copiado!");
        }
        GUILayout.EndHorizontal();

        GUILayout.Space(6);
        GUILayout.Label("Jogadores:");

        string localUid = SessionManager.Instance?.GetUserId() ?? "";
        int max = lobby?.maxPlayers ?? 4;

        for (int i = 0; i < max; i++)
        {
            var style = new GUIStyle(GUI.skin.label);
            string line;

            if (i < members.Count)
            {
                var m     = members[i];
                bool isMe   = !string.IsNullOrEmpty(localUid) && m.productUserId == localUid;
                bool isHost = !string.IsNullOrEmpty(lobby?.hostProductUserId) &&
                              m.productUserId == lobby.hostProductUserId;

                string charTag = m.selectedCharacterIndex >= 0 && m.selectedCharacterIndex < _charNames.Length
                    ? $" [{_charNames[m.selectedCharacterIndex]}]" : "";
                string tags = charTag +
                              (isHost    ? " [Host]" : "") +
                              (m.isReady ? " ✓"      : "") +
                              (isMe      ? " ◄ VOCÊ" : "");
                line = $"  {i + 1}. {m.displayName}{tags}";
                style.normal.textColor = isMe ? Color.yellow : m.isReady ? Color.green : Color.white;
            }
            else
            {
                line = $"  {i + 1}. — Aguardando —";
                style.normal.textColor = Color.gray;
            }
            GUILayout.Label(line, style);
        }

        GUILayout.Space(8);

        // Ready toggle
        bool novoReady = GUILayout.Toggle(_isReady, "  Estou Pronto");
        if (novoReady != _isReady)
        {
            _isReady = novoReady;
            _lobby.SetReady(_isReady);
        }

        GUILayout.Space(6);

        // Seleção de personagem
        GUILayout.Label("Personagem:");
        GUILayout.BeginHorizontal();
        for (int c = 0; c < _charNames.Length; c++)
        {
            GUI.backgroundColor = (_selectedChar == c) ? Color.cyan : Color.white;
            if (GUILayout.Button(_charNames[c], GUILayout.Width(88)))
            {
                _selectedChar = c;
                _lobby.SelectCharacter(c);
            }
        }
        GUI.backgroundColor = Color.white;
        GUILayout.EndHorizontal();

        GUILayout.Space(6);

        // Iniciar (host only)
        bool amHost = !string.IsNullOrEmpty(lobby?.hostProductUserId) &&
                      lobby.hostProductUserId == SessionManager.Instance?.GetUserId();
        if (amHost)
        {
            if (GUILayout.Button("Iniciar Partida", GUILayout.Height(42)))
            {
                AddLog("Iniciando partida...");
                _lobby.StartMatch();
            }
            GUILayout.Space(4);
        }

        if (GUILayout.Button("Sair da Sala", GUILayout.Height(34)))
        {
            AddLog("Saindo...");
            _lobby.LeaveLobby();
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Fluxo de inicialização EOS
    // ──────────────────────────────────────────────────────────────────────

    private IEnumerator InitMultiplayerFlow(bool openPanel = true)
    {
        _eosFlowRunning = true;

        AddLog("Aguardando EOS...");
        float elapsed = 0f;
        while (!EOSManagerWrapper.Instance.IsInitialized && elapsed < 15f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!EOSManagerWrapper.Instance.IsInitialized)
        {
            AddLog("Erro: EOS não inicializou.");
            _eosFlowRunning = false;
            yield break;
        }

        if (!EOSAuthenticator.Instance.IsLoggedIn)
        {
            AddLog("Fazendo login automático...");
            bool done = false, ok = false;
            Action<string> onSuccess = (_) => { done = true; ok = true; };
            Action<string> onFailed  = (_) => { done = true; ok = false; };
            EOSAuthenticator.Instance.OnLoginSuccess += onSuccess;
            EOSAuthenticator.Instance.OnLoginFailed  += onFailed;
            EOSAuthenticator.Instance.LoginWithDeviceId();

            elapsed = 0f;
            while (!done && elapsed < 30f) { elapsed += Time.deltaTime; yield return null; }

            EOSAuthenticator.Instance.OnLoginSuccess -= onSuccess;
            EOSAuthenticator.Instance.OnLoginFailed  -= onFailed;

            if (!ok)
            {
                AddLog("Falha no login EOS.");
                _eosFlowRunning = false;
                yield break;
            }
        }

        _eosFlowRunning = false;
        AddLog("Pronto! Crie ou entre em uma sala.");

        if (openPanel) AbrirPainelMultiplayer();
    }
}
