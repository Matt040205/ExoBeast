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
///   LobbyMenu    → seleção inicial: "Criar Sala (Host)" ou "Entrar em Sala (Cliente)"
///   HostConfig   → formulário de criação (nome, max jogadores, público/privado)
///   ClientSearch → formulário de entrada (por ID ou busca pública)
///   Sala         → sala de espera (jogadores, ready, personagem, iniciar)
/// </summary>
public class LobbySceneUI : MonoBehaviour
{
    private static string pendingStatusMessage;

    public static void SetPendingStatusMessage(string message)
    {
        pendingStatusMessage = message ?? "";
    }

    // ── Painéis ────────────────────────────────────────────────────────────
    // Fluxo: painel Lobby → painel CriarLobby → painel Jogadores
    [Header("Painéis (auto-detectados por nome se não atribuídos)")]
    [SerializeField] private GameObject painelLobby;       // Menu inicial: nick, login, criar/entrar
    [SerializeField] private GameObject painelCriarLobby;   // Config: nome, max jogadores, publico
    [SerializeField] private GameObject painelJogadores;    // Sala de espera: jogadores, iniciar

    [Header("Botões de navegação Host/Cliente")]
    [SerializeField] private Button btnCriarHost;
    [SerializeField] private Button btnEntrarCliente;
    [SerializeField] private Button btnVoltarHost;
    [SerializeField] private Button btnVoltarCliente;

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
    [SerializeField] private Button   iniciarPartidaButton;

    // ── Slots de Jogadores ────────────────────────────────────────────────
    [Header("Jogadores")]
    [SerializeField] private Transform  playerSlotsContent;
    [SerializeField] private GameObject playerSlotPrefab;

    // ── Estado Interno ────────────────────────────────────────────────────
    private enum State { LobbyMenu, HostConfig, Sala }

    private State  _state        = State.LobbyMenu;
    private string _lobbyId      = "";
    private string _lobbyNome    = "";
    private bool   _isReady      = false;
    private int    _maxPlayers   = 4;
    private bool   _eosRunning      = false;
    private bool   _isCreatingLobby = false;
    private Coroutine _createTimeoutCoroutine;

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
        DisableButtonLabelRaycasts();
    }

    private void Start()
    {
        SubscribeEvents();
        WireButtons();

        if (maxPlayersText != null) maxPlayersText.text = _maxPlayers.ToString();
        if (iniciarPartidaButton != null) iniciarPartidaButton.gameObject.SetActive(false);

        SetState(State.LobbyMenu);
        PreviewPendingStatusMessage();
        StartCoroutine(InitEOSFlow());
    }

    private void OnDestroy()
    {
        CancelCreateTimeout();
        UnsubscribeEvents();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Auto-detecção por nome (fallback se Inspector não preenchido)
    // ──────────────────────────────────────────────────────────────────────

    private void AutoDetectElements()
    {
        // Painéis — nomes batem com a Hierarchy da cena
        if (painelLobby      == null) painelLobby      = FindGO("painel Lobby");
        if (painelCriarLobby == null) painelCriarLobby = FindGO("painel CriarLobby");
        if (painelJogadores  == null) painelJogadores  = FindGO("painel Jogadores");

        // Botões de navegação
        if (btnCriarHost     == null) btnCriarHost     = FindIn<Button>("BtnCriarHost");
        if (btnEntrarCliente == null) btnEntrarCliente = FindIn<Button>("BtnEntrarCliente");
        if (btnVoltarHost    == null) btnVoltarHost    = FindIn<Button>("BtnVoltarHost");
        if (btnVoltarCliente == null) btnVoltarCliente = FindIn<Button>("BtnVoltarCliente");

        // InputFields
        if (nickField      == null) nickField      = FindIn<TMP_InputField>("DigitarNick");
        if (lobbyNameField == null) lobbyNameField = FindIn<TMP_InputField>("DigitarNomeSala");
        if (joinIdField    == null) joinIdField    = FindIn<TMP_InputField>("ProcurarLobbyID");
        if (joinIdField    == null) joinIdField    = FindIn<TMP_InputField>("PrcurarLobbyID");

        // Texto
        if (maxPlayersText == null) maxPlayersText = FindIn<TMP_Text>("MaxJogadores");
        if (statusText     == null) statusText     = FindIn<TMP_Text>("StatusText");
        if (lobbyCopyIdText == null) lobbyCopyIdText = FindIn<TMP_Text>("LobbyIdText");
        if (lobbyNameText   == null) lobbyNameText   = FindIn<TMP_Text>("LobbyNomeText");

        // Toggles
        if (publicoToggle == null) publicoToggle = FindIn<Toggle>("Publico/Privado");

        // Botões
        if (btnLogin    == null) btnLogin    = FindIn<Button>("BtnLogin");
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

    private void DisableButtonLabelRaycasts()
    {
        foreach (var button in GetComponentsInChildren<Button>(true))
        {
            foreach (var label in button.GetComponentsInChildren<TMP_Text>(true))
            {
                label.raycastTarget = false;
            }
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Wiring de botões por código (trata onClick nulos da cena)
    // ──────────────────────────────────────────────────────────────────────

    private void WireButtons()
    {
        // Auth
        WireBtn("BtnLogin",       Login);

        // Navegação
        WireBtn("BtnCriarHost",     AbrirModoHost);
        WireBtn("BtnVoltarHost",    VoltarParaMenu);

        // Criar Lobby (busca resiliente dentro do painel diretamente, ignorando espaços no nome do painel)
        if (painelCriarLobby != null)
        {
            foreach (var b in painelCriarLobby.GetComponentsInChildren<Button>(true))
            {
                if (b.gameObject.name == "CreateLobby" && b.transform.parent != null && b.transform.parent.name != "MaxJogadores")
                {
                    b.onClick = new Button.ButtonClickedEvent();
                    b.onClick.AddListener(CriarSala);
                }
                else if (b.gameObject.name == "CreateLobby" && b.transform.parent != null && b.transform.parent.name == "MaxJogadores")
                {
                    b.onClick = new Button.ButtonClickedEvent();
                    b.onClick.AddListener(() => AlterarMaxPlayers(-1));
                }
                else if (b.gameObject.name == "CreateLobby (1)")
                {
                    b.onClick = new Button.ButtonClickedEvent();
                    b.onClick.AddListener(() => AlterarMaxPlayers(+1));
                }
            }
        }
        
        // Mantem suporte a nomes corretos se arrumados
        WireBtn("-",              () => AlterarMaxPlayers(-1));
        WireBtn("+",              () => AlterarMaxPlayers(+1));
        WireBtn("'-'",            () => AlterarMaxPlayers(-1));
        WireBtn("'+'",            () => AlterarMaxPlayers(+1));

        // Entrar por ID (está no painel Lobby)
        WireBtn("BtnEntrarId",    EntrarPorId);
        WireBtn("EntrarId",       EntrarPorId);
        WireBtn("EntrarLobby",    EntrarPorId);

        // Buscar salas públicas (está no painel Lobby)
        WireBtn("BtnBuscarSalas", BuscarSalas);
        WireBtn("BuscarSalas",    BuscarSalas);
        WireBtn("LobbyPublico",   BuscarSalas);
        WireBtn("LobbyPulbico",   BuscarSalas);  // nome real na cena (typo)

        // Sala
        WireBtn("Copiar",         CopiarId);
        WireBtn("SairLobby",      SairDaSala);
        WireBtn("BtnSairLobby",   SairDaSala);
        WireBtn("BtnIniciarPartida", IniciarPartida);
        WireBtn("IniciarPartida",    IniciarPartida);

        // Voltar ao menu principal (sai da LobbyScene completamente)
        WireBtn("BtnVoltarMenuPrincipal", IrParaMenuPrincipal);
        WireBtn("VoltarMenuPrincipal",    IrParaMenuPrincipal);
        WireBtn("BtnMenu",                IrParaMenuPrincipal);
        WireBtn("BackMenu",               IrParaMenuPrincipal);  // nome real na cena

        // Inspector refs diretos
        if (btnLogin != null) { btnLogin.onClick = new Button.ButtonClickedEvent(); btnLogin.onClick.AddListener(Login); }
        if (iniciarPartidaButton != null) { iniciarPartidaButton.onClick = new Button.ButtonClickedEvent(); iniciarPartidaButton.onClick.AddListener(IniciarPartida); }

        // Auto-salva nick ao perder foco ou pressionar Enter (sem precisar de botão)
        if (nickField != null)
        {
            nickField.onEndEdit.RemoveAllListeners();
            nickField.onEndEdit.AddListener(_ => AtualizarNickLocal());
        }
    }

    private void WireBtn(string goName, Action handler)
    {
        foreach (var b in GetComponentsInChildren<Button>(true))
        {
            if (b.gameObject.name.Trim() != goName.Trim()) continue;
            // Cria um novo evento limpando tudo o que possa estar erradamente "injetado" no Inspecionar! (o GetPersistent não impede mais o código)
            b.onClick = new Button.ButtonClickedEvent();
            b.onClick.AddListener(() => handler());
        }
    }

    private void WireBtnByPath(string path, Action handler)
    {
        var tr = transform.Find(path);
        if (tr != null)
        {
            Debug.Log($"[LobbySceneUI] SUCESSO ao encontrar e mapear o botao via path: {path}");
            var b = tr.GetComponent<Button>();
            if (b != null)
            {
                // Substitui o UnityEvent por completo (ignora Inspecionar zoado)
                b.onClick = new Button.ButtonClickedEvent();
                b.onClick.AddListener(() => handler());
            }
        }
        else
        {
            Debug.LogError($"[LobbySceneUI] FALHA FATAL: Nao achou o botao no path '{path}'");
        }
    }

    private void WireBtnInParent(string parentName, string btnName, Action handler)
    {
        var parent = FindGO(parentName);
        if (parent == null) return;
        foreach (var b in parent.GetComponentsInChildren<Button>(true))
        {
            if (b.gameObject.name.Trim() != btnName.Trim()) continue;
            b.onClick = new Button.ButtonClickedEvent();
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

    private void AtualizarNickLocal()
    {
        if (nickField == null || string.IsNullOrWhiteSpace(nickField.text)) return;
        string nick = nickField.text.Trim();
        SessionManager.Instance?.SetDisplayName(nick);
        _auth?.SetDeviceIdName(nick);
        PlayerPrefs.SetString("PlayerDisplayName", nick);
        PlayerPrefs.Save();
    }

    public void CriarSala()
    {
        Debug.Log("[LobbySceneUI] CriarSala() foi ACIONADO pelo cloque do botao!");
        AtualizarNickLocal();

        if (_lobby == null) 
        { 
            Debug.LogError("[LobbySceneUI] CriarSala abortado: _lobby esta NULO!"); 
            return; 
        }
        if (_isCreatingLobby) 
        { 
            Debug.LogError("[LobbySceneUI] CriarSala ignorado: Ja esta em processo de criar lobby (_isCreatingLobby = true)."); 
            return; 
        }

        _isCreatingLobby = true;
        string nome = lobbyNameField != null && !string.IsNullOrWhiteSpace(lobbyNameField.text)
            ? lobbyNameField.text.Trim() : "Minha Sala";

        bool sucesso = _lobby.CreateLobby(new LobbySettings
        {
            lobbyName  = nome,
            maxPlayers = _maxPlayers,
            isPublic   = publicoToggle != null ? publicoToggle.isOn : true,
            mapName    = "EscolherPersonagem",
        });

        if (!sucesso) return;
        
        SetStatus("Criando sala...");

        // Timeout de segurança: se o callback EOS nunca disparar (ex: singleton
        // destruido na troca de cena), libera a UI apos 15 segundos.
        if (_createTimeoutCoroutine != null) StopCoroutine(_createTimeoutCoroutine);
        _createTimeoutCoroutine = StartCoroutine(CreateLobbyTimeoutCoroutine(15f));
    }

    private IEnumerator CreateLobbyTimeoutCoroutine(float timeout)
    {
        yield return new WaitForSeconds(timeout);
        if (_isCreatingLobby)
        {
            Debug.LogError($"[LobbySceneUI] Timeout de {timeout}s ao criar lobby — callback EOS nao retornou.");
            _isCreatingLobby = false;
            SetStatus("[ERRO] Tempo esgotado ao criar sala. Tente novamente.");
        }
        _createTimeoutCoroutine = null;
    }

    public void AlterarMaxPlayers(int delta)
    {
        _maxPlayers = Mathf.Clamp(_maxPlayers + delta, 2, 4);
        if (maxPlayersText != null) maxPlayersText.text = _maxPlayers.ToString();
    }

    public void AumentarMaxPlayers() => AlterarMaxPlayers(1);
    public void DiminuirMaxPlayers() => AlterarMaxPlayers(-1);

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
        AtualizarNickLocal();
        if (_lobby == null || joinIdField == null) return;
        string id = joinIdField.text.Trim();
        if (string.IsNullOrEmpty(id)) { SetStatus("Cole o ID da sala!"); return; }
        SetStatus($"Entrando...");
        _lobby.JoinLobby(id);
    }

    public void SelecionarPersonagem(int idx)
    {
        _lobby?.SelectCharacter(idx);
        SetStatus($"Personagem: {(idx < _charNames.Length ? _charNames[idx] : idx.ToString())}");
    }

    public void IniciarPartida()
    {
        SetStatus("Iniciando partida... aguardando rede");
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

    public void AbrirModoHost()    => SetState(State.HostConfig);
    public void VoltarParaMenu()   => SetState(State.LobbyMenu);

    public void IrParaMenuPrincipal()
    {
        StartCoroutine(ReturnToMenuPrincipalRoutine());
    }

    // Navegação Pública para vincular no Inspecionar do Unity
    public void IrParaCriarLobby()     => SetState(State.HostConfig);
    public void IrParaPainelJogadores() => SetState(State.Sala);

    // ──────────────────────────────────────────────────────────────────────
    // Eventos — LobbyManager
    // ──────────────────────────────────────────────────────────────────────

    private void OnLobbyCreated(LobbyInfo lobby)
    {
        _isCreatingLobby = false;
        CancelCreateTimeout();
        _lobbyId = lobby.lobbyId; _lobbyNome = lobby.lobbyName;
        SetState(State.Sala);
        AtualizarInfoSala();
        SetStatus($"Sala '{lobby.lobbyName}' criada!");
    }

    private void OnLobbyJoined(LobbyInfo lobby)
    {
        _lobbyId = lobby.lobbyId; _lobbyNome = lobby.lobbyName;
        SetState(State.Sala);
        AtualizarInfoSala();
        SetStatus($"Entrou em '{lobby.lobbyName}'");
    }

    private void OnLobbyLeft()
    {
        _lobbyId = ""; _lobbyNome = "";
        SetState(State.LobbyMenu);
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

    private void OnErro(string err)
    {
        _isCreatingLobby = false;
        CancelCreateTimeout();
        SetStatus($"[ERRO] {err}");
    }

    private void CancelCreateTimeout()
    {
        if (_createTimeoutCoroutine != null)
        {
            StopCoroutine(_createTimeoutCoroutine);
            _createTimeoutCoroutine = null;
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Eventos — EOSAuthenticator
    // ──────────────────────────────────────────────────────────────────────

    private void OnLoginSuccess(string userId)
    {
        string nick = PlayerPrefs.GetString("PlayerDisplayName", "");
        if (!string.IsNullOrEmpty(nick) && nickField != null) nickField.text = nick;
        AtualizarNickLocal();
        SetStatus("Pronto! Crie ou entre em uma sala.");
        TryApplyPendingStatusMessage();
    }

    private void OnLoginFailed(string err) => SetStatus($"Falha no login: {err}");

    // ──────────────────────────────────────────────────────────────────────
    // Gerenciamento de Estado / Painéis
    // ──────────────────────────────────────────────────────────────────────

    private void SetState(State s)
    {
        _state = s;

        bool inMenu = s == State.LobbyMenu;
        bool inHost = s == State.HostConfig;
        bool inSala = s == State.Sala;

        // Cada estado mostra exatamente UM painel
        if (painelLobby      != null) painelLobby.SetActive(inMenu);
        if (painelCriarLobby != null) painelCriarLobby.SetActive(inHost);
        if (painelJogadores  != null) painelJogadores.SetActive(inSala);

        // Nick e login visíveis apenas no menu inicial
        if (nickField != null) nickField.gameObject.SetActive(inMenu);
        if (btnLogin  != null) btnLogin.gameObject.SetActive(inMenu);

        if (inSala) AtualizarSlots();
    }

    private void AtualizarInfoSala()
    {
        if (lobbyNameText   != null) lobbyNameText.text   = _lobbyNome;
        if (lobbyCopyIdText != null) lobbyCopyIdText.text = _lobbyId;
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
        if (isHost)
        {
            iniciarPartidaButton.interactable = AllMembersReady();
        }
    }

    private bool AllMembersReady()
    {
        if (_lobby == null) return false;
        var members = _lobby.GetMembers();
        if (members == null || members.Count == 0) return false;
        return members.TrueForAll(m => m.isReady);
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

        // Ajusta contagem de slots.
        // NOTA: Destroy() é diferido — childCount não diminui no mesmo frame.
        // Usar while+Destroy causaria loop infinito. O for com bounds fixas é seguro.
        while (playerSlotsContent.childCount < max) CriarSlotVazio();
        for (int i = playerSlotsContent.childCount - 1; i >= max; i--)
            Destroy(playerSlotsContent.GetChild(i).gameObject);

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
            AtualizarNickLocal();
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

    private void PreviewPendingStatusMessage()
    {
        if (!string.IsNullOrEmpty(pendingStatusMessage))
            SetStatus(pendingStatusMessage);
    }

    private bool TryApplyPendingStatusMessage()
    {
        if (string.IsNullOrEmpty(pendingStatusMessage))
            return false;

        SetStatus(pendingStatusMessage);
        pendingStatusMessage = "";
        return true;
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

        // Aguarda EOS inicializar, mas sem bloquear a UI — ela já está em LobbyList
        SetStatus("Conectando ao EOS...");
        float elapsed = 0f;
        while (!EOSManagerWrapper.Instance.IsInitialized && elapsed < 15f)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }

        if (!EOSManagerWrapper.Instance.IsInitialized)
        {
            // EOS offline: avisa mas não trava — testes de UI ainda funcionam
            SetStatus("EOS offline — operações de rede indisponíveis.");
            _eosRunning = false;
            yield break;
        }

        // Re-cacheia singletons (podem ter sido criados durante o wait)
        if (_auth  == null) _auth  = EOSAuthenticator.Instance;
        if (_lobby == null) _lobby = LobbyManager.Instance;
        UnsubscribeEvents();
        SubscribeEvents();

        if (_auth.IsLoggedIn)
        {
            string savedNick = PlayerPrefs.GetString("PlayerDisplayName", "");
            if (!string.IsNullOrEmpty(savedNick) && nickField != null) nickField.text = savedNick;
            SetStatus("Pronto! Crie ou entre em uma sala.");
            TryApplyPendingStatusMessage();
            _eosRunning = false;
            yield break;
        }

        // Auto-login: usa nick salvo ou gera nome automatico na primeira execucao
        string autoNick = PlayerPrefs.GetString("PlayerDisplayName", "");
        if (string.IsNullOrEmpty(autoNick))
            autoNick = "Jogador_" + UnityEngine.Random.Range(1000, 9999);

        if (nickField != null) nickField.text = autoNick;
        SessionManager.Instance?.SetDisplayName(autoNick);
        _auth.SetDeviceIdName(autoNick);
        PlayerPrefs.SetString("PlayerDisplayName", autoNick);
        PlayerPrefs.Save();

        SetStatus($"Entrando como '{autoNick}'...");
        _auth.LoginWithDeviceId();

        _eosRunning = false;
    }

    private IEnumerator ReturnToMenuPrincipalRoutine()
    {
        Debug.Log("[LobbySceneUI] Voltando para Menu Principal...");

        CancelCreateTimeout();
        UnsubscribeEvents();

        yield return MultiplayerRuntimeReset.ResetToOfflineLocal();
        GameModeManager.ReturnToSingleplayer();
        UnityEngine.SceneManagement.SceneManager.LoadScene("MenuScene");
    }
}
