using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using ExoBeasts.Multiplayer.Lobby;
using ExoBeasts.Multiplayer.Auth;
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Managers;

/// <summary>
/// Controlador Canvas-based para Assets/Scenes/LobbyScene.unity.
/// Auto-detecta elementos por nome no Awake; [SerializeField] permite override no Inspector.
///
/// Estados:
///   Auth      → mostra campo nick + botão Jogar, aguarda EOS
///   LobbyList → cria/busca/entra em salas
///   Sala      → sala de espera (jogadores, ready, personagem, iniciar)
/// </summary>
public class LobbySceneUI : MonoBehaviour
{
    // ── Painéis ────────────────────────────────────────────────────────────
    [Header("Painéis (auto-detectados por nome se não atribuídos)")]
    [SerializeField] private GameObject painelCriar;
    [SerializeField] private GameObject painelSala;
    [SerializeField] private GameObject painelJogadores;

    // ── Auth ───────────────────────────────────────────────────────────────
    [Header("Auth")]
    [SerializeField] private TMP_InputField nickField;
    [SerializeField] private Button         btnLogin;
    [SerializeField] private TMP_Text       statusText;

    // ── Criar / Buscar Salas ──────────────────────────────────────────────
    [Header("Criar Lobby")]
    [SerializeField] private TMP_InputField lobbyNameField;
    [SerializeField] private TMP_Text       maxPlayersText;
    [SerializeField] private Toggle         publicoToggle;
    [SerializeField] private TMP_InputField joinIdField;
    [SerializeField] private Transform      lobbyListContent;
    [SerializeField] private GameObject     lobbyCardPrefab;

    // ── Sala de Espera ────────────────────────────────────────────────────
    [Header("Sala")]
    [SerializeField] private TMP_Text lobbyCopyIdText;
    [SerializeField] private TMP_Text lobbyNameText;
    [SerializeField] private Toggle   readyToggle;
    [SerializeField] private Button   iniciarPartidaButton;
    [SerializeField] private Button   btnCoruja;
    [SerializeField] private Button   btnSamurai;

    // ── Slots de Jogadores ────────────────────────────────────────────────
    [Header("Jogadores")]
    [SerializeField] private Transform  playerSlotsContent;
    [SerializeField] private GameObject playerSlotPrefab;

    // ── Estado Interno ────────────────────────────────────────────────────
    private enum State { Auth, LobbyList, Sala }

    private State  _state        = State.Auth;
    private string _lobbyId      = "";
    private string _lobbyNome    = "";
    private bool   _isReady      = false;
    private int    _selectedChar = -1;
    private int    _maxPlayers   = 4;
    private bool   _eosRunning   = false;

    private static readonly string[] _charNames = { "Coruja", "Samurai" };

    private LobbyManager     _lobby;
    private EOSAuthenticator _auth;

    // ──────────────────────────────────────────────────────────────────────
    // Ciclo de vida
    // ──────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        _lobby = LobbyManager.Instance;
        _auth  = EOSAuthenticator.Instance;
        AutoDetectElements();
    }

    private void Start()
    {
        SubscribeEvents();
        WireButtons();

        if (maxPlayersText != null) maxPlayersText.text = _maxPlayers.ToString();
        if (iniciarPartidaButton != null) iniciarPartidaButton.gameObject.SetActive(false);

        SetState(State.Auth);
        StartCoroutine(InitEOSFlow());
    }

    private void OnDestroy() => UnsubscribeEvents();

    // ──────────────────────────────────────────────────────────────────────
    // Auto-detecção por nome (fallback se Inspector não preenchido)
    // ──────────────────────────────────────────────────────────────────────

    private void AutoDetectElements()
    {
        // Painéis
        if (painelCriar     == null) painelCriar     = FindGO("painel CriarLobby");
        if (painelCriar     == null) painelCriar     = FindGO("painel CriarLobby ");
        if (painelSala      == null) painelSala      = FindGO("painel Lobby");
        if (painelJogadores == null) painelJogadores = FindGO("painel Jogadores");

        // InputFields
        if (nickField      == null) nickField      = FindIn<TMP_InputField>("DigitarNick");
        if (lobbyNameField == null) lobbyNameField = FindIn<TMP_InputField>("DigitarNomeSala");
        if (joinIdField    == null) joinIdField    = FindIn<TMP_InputField>("PrcurarLobbyID");

        // Texto
        if (maxPlayersText == null) maxPlayersText = FindIn<TMP_Text>("MaxJogadores");
        if (statusText     == null) statusText     = FindIn<TMP_Text>("StatusText");
        if (lobbyCopyIdText == null) lobbyCopyIdText = FindIn<TMP_Text>("LobbyIdText");
        if (lobbyNameText   == null) lobbyNameText   = FindIn<TMP_Text>("LobbyNomeText");

        // Toggles
        if (publicoToggle == null) publicoToggle = FindIn<Toggle>("Publico/Privado");
        if (readyToggle   == null) readyToggle   = FindIn<Toggle>("Pronto");

        // Botões
        if (btnLogin    == null) btnLogin    = FindIn<Button>("BtnLogin");
        if (btnCoruja   == null) btnCoruja   = FindIn<Button>("BtnCoruja");
        if (btnSamurai  == null) btnSamurai  = FindIn<Button>("BtnSamurai");
        if (iniciarPartidaButton == null) iniciarPartidaButton = FindIn<Button>("BtnIniciarPartida");

        // Slots / Lista
        if (playerSlotsContent == null)
        {
            var go = FindGO("PlayerSlotsContent");
            if (go != null) playerSlotsContent = go.transform;
        }
        if (lobbyListContent == null)
        {
            var go = FindGO("LobbyListContent");
            if (go != null) lobbyListContent = go.transform;
        }
    }

    private T FindIn<T>(string goName) where T : Component
    {
        foreach (var c in GetComponentsInChildren<T>(true))
            if (c.gameObject.name.Trim() == goName.Trim()) return c;
        return null;
    }

    private GameObject FindGO(string goName)
    {
        foreach (var t in GetComponentsInChildren<Transform>(true))
            if (t.gameObject.name.Trim() == goName.Trim()) return t.gameObject;
        return null;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Wiring de botões por código (trata onClick nulos da cena)
    // ──────────────────────────────────────────────────────────────────────

    private void WireButtons()
    {
        // Auth
        WireBtn("BtnLogin",       Login);

        // Criar Lobby
        WireBtnInParent("painel CriarLobby", "CreateLobby", CriarSala);
        WireBtnInParent("painel CriarLobby ", "CreateLobby", CriarSala);
        WireBtn("-",              () => AlterarMaxPlayers(-1));
        WireBtn("+",              () => AlterarMaxPlayers(+1));
        WireBtn("'-'",            () => AlterarMaxPlayers(-1));
        WireBtn("'+'",            () => AlterarMaxPlayers(+1));

        // Buscar
        WireBtn("BtnBuscarSalas", BuscarSalas);
        WireBtn("BuscarSalas",    BuscarSalas);

        // Entrar por ID
        WireBtn("BtnEntrarId",    EntrarPorId);
        WireBtn("EntrarId",       EntrarPorId);

        // Sala
        WireBtn("Copiar",         CopiarId);
        WireBtn("SairLobby",      SairDaSala);
        WireBtn("BtnSairLobby",   SairDaSala);
        WireBtn("BtnIniciarPartida", IniciarPartida);
        WireBtn("BtnCoruja",      () => SelecionarPersonagem(0));
        WireBtn("BtnSamurai",     () => SelecionarPersonagem(1));

        // Toggles
        if (readyToggle != null)
        {
            readyToggle.onValueChanged.RemoveAllListeners();
            readyToggle.onValueChanged.AddListener(val => { _isReady = val; _lobby?.SetReady(_isReady); });
        }

        // Inspector refs diretos
        if (btnLogin    != null) btnLogin.onClick.AddListener(Login);
        if (btnCoruja   != null) btnCoruja.onClick.AddListener(() => SelecionarPersonagem(0));
        if (btnSamurai  != null) btnSamurai.onClick.AddListener(() => SelecionarPersonagem(1));
        if (iniciarPartidaButton != null) iniciarPartidaButton.onClick.AddListener(IniciarPartida);
    }

    private void WireBtn(string goName, Action handler)
    {
        foreach (var b in GetComponentsInChildren<Button>(true))
        {
            if (b.gameObject.name.Trim() != goName.Trim()) continue;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => handler());
        }
    }

    private void WireBtnInParent(string parentName, string btnName, Action handler)
    {
        var parent = FindGO(parentName);
        if (parent == null) return;
        foreach (var b in parent.GetComponentsInChildren<Button>(true))
        {
            if (b.gameObject.name.Trim() != btnName.Trim()) continue;
            b.onClick.RemoveAllListeners();
            b.onClick.AddListener(() => handler());
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // API pública — botões do Inspector chamam estes
    // ──────────────────────────────────────────────────────────────────────

    public void Login()
    {
        if (_auth == null) return;
        string nick = nickField != null ? nickField.text.Trim() : "";
        if (string.IsNullOrEmpty(nick)) { SetStatus("Digite um nick!"); return; }

        SessionManager.Instance?.SetDisplayName(nick);
        _auth.SetDeviceIdName(nick);
        PlayerPrefs.SetString("PlayerDisplayName", nick);
        PlayerPrefs.Save();
        SetStatus("Fazendo login...");
        _auth.LoginWithDeviceId();
    }

    public void CriarSala()
    {
        if (_lobby == null) return;
        string nome = lobbyNameField != null && !string.IsNullOrWhiteSpace(lobbyNameField.text)
            ? lobbyNameField.text.Trim() : "Minha Sala";

        _lobby.CreateLobby(new LobbySettings
        {
            lobbyName  = nome,
            maxPlayers = _maxPlayers,
            isPublic   = publicoToggle != null ? publicoToggle.isOn : true,
            mapName    = "SceneMapTest",
        });
        SetStatus("Criando sala...");
    }

    public void AlterarMaxPlayers(int delta)
    {
        _maxPlayers = Mathf.Clamp(_maxPlayers + delta, 2, 4);
        if (maxPlayersText != null) maxPlayersText.text = _maxPlayers.ToString();
    }

    public void BuscarSalas()
    {
        if (_lobby == null) return;
        LimparCardsLobby();
        SetStatus("Buscando salas públicas...");
        _lobby.SearchLobbies(new LobbySearchFilter { onlyPublic = true, maxResults = 10 });
    }

    public void AtualizarSalas() => BuscarSalas();

    public void EntrarPorId()
    {
        if (_lobby == null || joinIdField == null) return;
        string id = joinIdField.text.Trim();
        if (string.IsNullOrEmpty(id)) { SetStatus("Cole o ID da sala!"); return; }
        SetStatus($"Entrando...");
        _lobby.JoinLobby(id);
    }

    public void ToggleReady()
    {
        _isReady = !_isReady;
        if (readyToggle != null) readyToggle.SetIsOnWithoutNotify(_isReady);
        _lobby?.SetReady(_isReady);
    }

    public void SelecionarPersonagem(int idx)
    {
        _selectedChar = idx;
        _lobby?.SelectCharacter(idx);
        SetStatus($"Personagem: {(idx < _charNames.Length ? _charNames[idx] : idx.ToString())}");
    }

    public void IniciarPartida()
    {
        SetStatus("Iniciando partida...");
        _lobby?.StartMatch();
    }

    public void SairDaSala()
    {
        SetStatus("Saindo da sala...");
        _lobby?.LeaveLobby();
    }

    public void CopiarId()
    {
        GUIUtility.systemCopyBuffer = _lobbyId;
        SetStatus("ID copiado para a área de transferência!");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Eventos — LobbyManager
    // ──────────────────────────────────────────────────────────────────────

    private void OnLobbyCreated(LobbyInfo lobby)
    {
        _lobbyId = lobby.lobbyId; _lobbyNome = lobby.lobbyName;
        _isReady = false; _selectedChar = -1;
        SetState(State.Sala);
        AtualizarInfoSala();
        SetStatus($"Sala '{lobby.lobbyName}' criada!");
    }

    private void OnLobbyJoined(LobbyInfo lobby)
    {
        _lobbyId = lobby.lobbyId; _lobbyNome = lobby.lobbyName;
        _isReady = false; _selectedChar = -1;
        SetState(State.Sala);
        AtualizarInfoSala();
        SetStatus($"Entrou em '{lobby.lobbyName}'");
    }

    private void OnLobbyLeft()
    {
        _lobbyId = ""; _lobbyNome = "";
        _isReady = false; _selectedChar = -1;
        SetState(State.LobbyList);
        SetStatus("Saiu da sala.");
    }

    private void OnLobbiesFound(List<LobbyInfo> lobbies)
    {
        SetStatus($"{lobbies.Count} sala(s) encontrada(s).");
        LimparCardsLobby();
        foreach (var l in lobbies) CriarCardLobby(l);
    }

    private void OnMemberEvento(LobbyMember _)
    {
        if (_state == State.Sala) AtualizarSlots();
    }

    private void OnErro(string err) => SetStatus($"[ERRO] {err}");

    // ──────────────────────────────────────────────────────────────────────
    // Eventos — EOSAuthenticator
    // ──────────────────────────────────────────────────────────────────────

    private void OnLoginSuccess(string userId)
    {
        string nick = PlayerPrefs.GetString("PlayerDisplayName", "");
        if (!string.IsNullOrEmpty(nick) && nickField != null) nickField.text = nick;
        SetState(State.LobbyList);
        SetStatus("Pronto! Crie ou entre em uma sala.");
    }

    private void OnLoginFailed(string err) => SetStatus($"Falha no login: {err}");

    // ──────────────────────────────────────────────────────────────────────
    // Gerenciamento de Estado / Painéis
    // ──────────────────────────────────────────────────────────────────────

    private void SetState(State s)
    {
        _state = s;

        bool inSala  = (s == State.Sala);
        bool inCriar = !inSala;

        if (painelCriar     != null) painelCriar.SetActive(inCriar);
        if (painelSala      != null) painelSala.SetActive(inSala);
        if (painelJogadores != null) painelJogadores.SetActive(inSala);

        // Botão Login só visível no estado Auth
        if (btnLogin != null) btnLogin.gameObject.SetActive(s == State.Auth);

        if (inSala) AtualizarSlots();
    }

    private void AtualizarInfoSala()
    {
        if (lobbyNameText   != null) lobbyNameText.text   = _lobbyNome;
        if (lobbyCopyIdText != null) lobbyCopyIdText.text = _lobbyId;
        if (readyToggle     != null) readyToggle.SetIsOnWithoutNotify(false);
        AtualizarBotaoIniciar();
    }

    private void AtualizarBotaoIniciar()
    {
        if (iniciarPartidaButton == null) return;
        var lobby = _lobby?.GetCurrentLobby();
        string localUid = SessionManager.Instance?.GetUserId() ?? "";
        bool isHost = lobby != null
            && !string.IsNullOrEmpty(lobby.hostProductUserId)
            && lobby.hostProductUserId == localUid;
        iniciarPartidaButton.gameObject.SetActive(isHost);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Slots de Jogadores (dinâmicos)
    // ──────────────────────────────────────────────────────────────────────

    private void AtualizarSlots()
    {
        if (playerSlotsContent == null) return;

        var lobby   = _lobby?.GetCurrentLobby();
        var members = _lobby?.GetMembers() ?? new List<LobbyMember>();
        string localUid = SessionManager.Instance?.GetUserId() ?? "";
        int max = lobby?.maxPlayers ?? 4;

        // Ajusta contagem de slots
        while (playerSlotsContent.childCount < max) CriarSlotVazio();
        while (playerSlotsContent.childCount > max)
            Destroy(playerSlotsContent.GetChild(playerSlotsContent.childCount - 1).gameObject);

        for (int i = 0; i < max; i++)
        {
            var slot = playerSlotsContent.GetChild(i);
            var txt  = slot.GetComponentInChildren<TMP_Text>();
            if (txt == null) continue;

            if (i < members.Count)
            {
                var m      = members[i];
                bool isMe  = m.productUserId == localUid;
                bool amHost = lobby != null && m.productUserId == lobby.hostProductUserId;

                string charTag = m.selectedCharacterIndex >= 0
                    && m.selectedCharacterIndex < _charNames.Length
                    ? $" [{_charNames[m.selectedCharacterIndex]}]" : "";
                string tags = charTag
                    + (amHost  ? " [Host]" : "")
                    + (m.isReady ? " ✓"   : "")
                    + (isMe    ? " ◄ VOCÊ" : "");

                txt.text  = $"{i + 1}. {m.displayName}{tags}";
                txt.color = isMe ? Color.yellow : m.isReady ? Color.green : Color.white;
            }
            else
            {
                txt.text  = $"{i + 1}. — Aguardando —";
                txt.color = Color.gray;
            }
        }

        AtualizarBotaoIniciar();
    }

    private void CriarSlotVazio()
    {
        if (playerSlotPrefab != null)
        {
            Instantiate(playerSlotPrefab, playerSlotsContent);
            return;
        }
        // Fallback: texto simples
        var go  = new GameObject("PlayerSlot", typeof(RectTransform));
        go.transform.SetParent(playerSlotsContent, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(400, 40);
        var txtGo  = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
        txtGo.transform.SetParent(go.transform, false);
        var txt = txtGo.AddComponent<TextMeshProUGUI>();
        txt.fontSize = 18;
        var txtRect = txtGo.GetComponent<RectTransform>();
        txtRect.anchorMin = Vector2.zero;
        txtRect.anchorMax = Vector2.one;
        txtRect.sizeDelta = Vector2.zero;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Cards de Lobby (dinâmicos)
    // ──────────────────────────────────────────────────────────────────────

    private void LimparCardsLobby()
    {
        if (lobbyListContent == null) return;
        foreach (Transform t in lobbyListContent) Destroy(t.gameObject);
    }

    private void CriarCardLobby(LobbyInfo info)
    {
        if (lobbyListContent == null) return;

        GameObject card;
        if (lobbyCardPrefab != null)
        {
            card = Instantiate(lobbyCardPrefab, lobbyListContent);
        }
        else
        {
            card = new GameObject($"Card_{info.lobbyName}", typeof(RectTransform));
            card.transform.SetParent(lobbyListContent, false);
            var rect = card.GetComponent<RectTransform>();
            rect.sizeDelta = new Vector2(500, 50);
            var layout = card.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 10;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth  = false;

            // Nome
            AddText(card, info.lobbyName, 180, 18, Color.white);
            // Capacidade
            AddText(card, $"{info.currentPlayers}/{info.maxPlayers}", 60, 16, Color.cyan);
        }

        // Botão Entrar
        var btn = card.GetComponentInChildren<Button>();
        if (btn == null)
        {
            var btnGo = new GameObject("BtnEntrar", typeof(RectTransform), typeof(CanvasRenderer));
            btnGo.transform.SetParent(card.transform, false);
            var btnRect = btnGo.GetComponent<RectTransform>();
            btnRect.sizeDelta = new Vector2(80, 36);
            var img = btnGo.AddComponent<Image>();
            img.color = new Color(0.2f, 0.6f, 1f, 1f);
            btn = btnGo.AddComponent<Button>();
            AddText(btnGo, "Entrar", 80, 16, Color.white);
        }

        bool cheio = info.currentPlayers >= info.maxPlayers;
        btn.interactable = !cheio;
        string lobbyId = info.lobbyId;
        btn.onClick.RemoveAllListeners();
        btn.onClick.AddListener(() =>
        {
            SetStatus($"Entrando em '{info.lobbyName}'...");
            _lobby?.JoinLobby(lobbyId);
        });
    }

    private static GameObject AddText(GameObject parent, string content, float width, float size, Color color)
    {
        var go   = new GameObject("Text", typeof(RectTransform), typeof(CanvasRenderer));
        go.transform.SetParent(parent.transform, false);
        var rect = go.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(width, 40);
        var txt = go.AddComponent<TextMeshProUGUI>();
        txt.text      = content;
        txt.fontSize  = size;
        txt.color     = color;
        txt.alignment = TextAlignmentOptions.MidlineLeft;
        return go;
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private void SetStatus(string msg)
    {
        if (statusText != null) statusText.text = msg;
        Debug.Log($"[LobbySceneUI] {msg}");
    }

    private void SubscribeEvents()
    {
        if (_auth != null)
        {
            _auth.OnLoginSuccess += OnLoginSuccess;
            _auth.OnLoginFailed  += OnLoginFailed;
        }
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

    private void UnsubscribeEvents()
    {
        if (_auth != null)
        {
            _auth.OnLoginSuccess -= OnLoginSuccess;
            _auth.OnLoginFailed  -= OnLoginFailed;
        }
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

    // ──────────────────────────────────────────────────────────────────────
    // Fluxo EOS — aguarda init e faz login automático
    // ──────────────────────────────────────────────────────────────────────

    private IEnumerator InitEOSFlow()
    {
        if (_eosRunning) yield break;
        _eosRunning = true;

        SetStatus("Aguardando EOS...");
        float elapsed = 0f;
        while (!EOSManagerWrapper.Instance.IsInitialized && elapsed < 15f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!EOSManagerWrapper.Instance.IsInitialized)
        {
            SetStatus("Erro: EOS não inicializou. Verifique a conexão.");
            _eosRunning = false;
            yield break;
        }

        // Re-cacheia singletons (podem ter sido criados durante o wait)
        if (_auth  == null) _auth  = EOSAuthenticator.Instance;
        if (_lobby == null) _lobby = LobbyManager.Instance;
        SubscribeEvents();

        if (_auth.IsLoggedIn)
        {
            OnLoginSuccess("");
            _eosRunning = false;
            yield break;
        }

        // Pré-preenche nick salvo
        string saved = PlayerPrefs.GetString("PlayerDisplayName", "");
        if (!string.IsNullOrEmpty(saved) && nickField != null)
            nickField.text = saved;

        SetStatus("Digite seu nick e clique Jogar.");
        _eosRunning = false;
    }
}
