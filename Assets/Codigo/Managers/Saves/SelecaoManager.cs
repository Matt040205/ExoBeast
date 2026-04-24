using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using Unity.Collections;
using DG.Tweening;
using ExoBeasts.Managers;
using ExoBeasts.Multiplayer.Lobby;
using ExoBeasts.Multiplayer.Core;

/// <summary>
/// ── SelecaoManager ─────────────────────────────────────
/// Gerencia a interface de equipe, seleção de personagens e sincronização multiplayer.
/// 
///  ▸ Divisão de slots por autoridade de jogador.
///  ▸ Validação de estado do botão Play (requer Slot 0 e 1).
///  ▸ Sincronização via ServerRpc e ClientRpc garantindo registro no GameDataManager.
/// ───────────────────────────────────────────────────────
/// </summary>
public class SelecaoManager : NetworkBehaviour
{
    public static SelecaoManager Instance;
    public List<CharacterBase> todosOsPersonagens;

    [Header("Paineis")]
    public GameObject painelEquipe;
    public GameObject painelEscolhaPersonagem;
    public GameObject painelDetalhes;

    [Header("UI Principal")]
    public GameObject slotEquipePrefab;
    public Transform gridEquipeContainer;
    public Button botaoJogar;
    public Toggle togglePronto;
    public string nomeDaCenaDoJogo = "CenaMapaTeste";

    [Header("Lista de Jogadores da Sala")]
    public GameObject painelJogadoresLobby; // O painel base que contém a lista
    public Transform containerListaJogadores;
    public GameObject prefabSlotJogadorLobby;

    [Header("Modo Remover")]
    public Button botaoRemover;
    public Color corModoRemover = Color.red;
    private bool isRemoveMode = false;
    private Color corOriginalBotaoRemover;

    [Header("Selecao de Personagem")]
    public GameObject slotEscolhaPrefab;
    public Transform gridEscolhaContainer;
    public Button botaoVoltarDaEscolha;

    [Header("Detalhes")]
    public Image imagemDetalhes;
    public TextMeshProUGUI nomeDetalhes;
    public TextMeshProUGUI textoStatusPadrao;
    public Button botaoConfirmarEscolha;
    public Button botaoVoltarDosDetalhes;

    [Header("Abas e Textos")]
    public GameObject painelHabilidades;
    public GameObject painelUpgradesTorre;
    public TextMeshProUGUI textoHabilidadesComandante;
    public TextMeshProUGUI textoCaminho1, textoCaminho2, textoCaminho3;
    public List<Button> botoesCaminhoTorre;

    private List<SlotEquipeUI> slotsEquipe = new List<SlotEquipeUI>();
    private Dictionary<CharacterBase, Button> botoesDeEscolha = new Dictionary<CharacterBase, Button>();
    private int slotSendoEditado = -1;
    private CharacterBase personagemEmVisualizacao;

    private List<int> slotsPermitidos = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };

    [Header("Cores dos Jogadores")]
    public Color[] coresPorJogador = new Color[] {
        new Color(0.2f, 0.6f, 1f, 1f), // P1 Azul
        new Color(1f, 0.4f, 0.4f, 1f), // P2 Vermelho
        new Color(0.4f, 1f, 0.4f, 1f), // P3 Verde
        new Color(1f, 1f, 0.4f, 1f)    // P4 Amarelo
    };

    private bool _isReady = false;

    // Custom message key — lets clients register their character choice on the server
    // without needing this NetworkBehaviour to be spawned as a NetworkObject in the scene.
    private const string k_CharChoiceMsg = "ExoBeasts.CharacterChoice";

    private void Awake() => Instance = this;

    void Start()
    {
        if (botaoRemover != null) corOriginalBotaoRemover = botaoRemover.image.color;

        // Inscreve nos eventos do lobby para detectar quando todos estão prontos
        if (GameModeManager.CurrentMode == GameMode.Multiplayer && LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnMemberUpdated += OnMemberUpdatedCheck;
            LobbyManager.Instance.OnMemberJoined  += OnMemberUpdatedCheck;
        }

        // Registra handler de escolha de personagem no servidor.
        // CustomMessagingManager não requer NetworkObject spawnado — funciona se NGO estiver ativo.
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.CustomMessagingManager.RegisterNamedMessageHandler(k_CharChoiceMsg, OnCharacterChoiceReceived);

        StartCoroutine(SetupScene());
    }

    private void OnDestroy()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnMemberUpdated -= OnMemberUpdatedCheck;
            LobbyManager.Instance.OnMemberJoined  -= OnMemberUpdatedCheck;
        }

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer)
            NetworkManager.Singleton.CustomMessagingManager.UnregisterNamedMessageHandler(k_CharChoiceMsg);
    }

    public override void OnNetworkSpawn() => CalcularLimitesDeSlots();

    IEnumerator SetupScene()
    {
        LimparGrid(gridEquipeContainer);
        LimparGrid(gridEscolhaContainer);
        slotsEquipe.Clear();
        botoesDeEscolha.Clear();

        yield return new WaitUntil(() => GameDataManager.Instance != null);

        todosOsPersonagens = new List<CharacterBase>(GameDataManager.Instance.personagensDoJogador);

        // GameDataManager.Instance.RestaurarSelecao();
        GameDataManager.Instance.LimparSelecao();
        
        ConfigurarBotoesPrincipais();
        CriarGridEquipe();
        PopularGridDeEscolha();

        painelEquipe.SetActive(true);
        AtualizarEstadoBotaoJogar();
        AtualizarListaJogadores();

        // TUTORIAL: Ao iniciar a cena, explica como escolher o comandante
        if (TutorialManager.Instance != null)
        {
            // Se ja voltou dos Rastros, mostra GO_TO_ACTION
            if (GameDataManager.Instance.tutoriaisConcluidos.Contains("RETURN_TO_SELECTION"))
                TutorialManager.Instance.TriggerTutorial("GO_TO_ACTION");
            else
                TutorialManager.Instance.TriggerTutorial("SELECT_COMMANDER");
        }
    }

    public void CalcularLimitesDeSlots()
    {
        if (NetworkManager.Singleton == null || (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer))
        {
            slotsPermitidos = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
            return;
        }

        // Em singleplayer, LobbyManager/SessionManager não estão inicializados.
        if (GameModeManager.CurrentMode == GameMode.Singleplayer)
        {
            slotsPermitidos = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
            return;
        }

        var lobbyManager = ExoBeasts.Multiplayer.Lobby.LobbyManager.Instance;
        var membros = lobbyManager.GetOrderedMembers();
        string meuId = ExoBeasts.Multiplayer.Auth.SessionManager.Instance.GetUserId();
        int meuIndice = lobbyManager.GetCanonicalMemberIndex(meuId);
        int total = membros.Count;

        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.totalDeJogadores = total;
        }

        if (meuIndice == -1) return;

        slotsPermitidos = ObterSlotsDoJogador(total, meuIndice);
    }

    private List<int> ObterSlotsDoJogador(int totalJogadores, int indiceJogador)
    {
        return PartySlotLayout.GetSlots(totalJogadores, indiceJogador);
    }

    void CriarGridEquipe()
    {
        int totalJogadores = 1;
        if (GameDataManager.Instance != null && GameDataManager.Instance.totalDeJogadores > 1) {
            totalJogadores = GameDataManager.Instance.totalDeJogadores;
        } else if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer)) {
            totalJogadores = ExoBeasts.Multiplayer.Lobby.LobbyManager.Instance.GetMembers().Count;
        }

        for (int i = 0; i < 8; i++)
        {
            GameObject obj = Instantiate(slotEquipePrefab, gridEquipeContainer);
            SlotEquipeUI ui = obj.GetComponent<SlotEquipeUI>();
            int idx = i;
            obj.GetComponent<Button>().onClick.AddListener(() => OnSlotClicked(idx));

            if (GameDataManager.Instance.equipeSelecionada[i] != null)
                ui.SetPersonagem(GameDataManager.Instance.equipeSelecionada[i]);
            else
                ui.LimparSlot();

            int dono = -1;
            for(int p = 0; p < totalJogadores; p++) {
                if (ObterSlotsDoJogador(totalJogadores, p).Contains(idx)) { dono = p; break; }
            }
            if(dono >= 0 && dono < coresPorJogador.Length) ui.DefinirCorDoJogador(coresPorJogador[dono]);
            else ui.DefinirCorDoJogador(Color.white);

            slotsEquipe.Add(ui);
        }
    }

    // TRAVA DE SEGURANÇA: Garante que só tente usar RPC se o NGO estiver rodando E o manager estiver spawnado na rede
    private bool IsNetworkActive =>
        NetworkManager.Singleton != null &&
        (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer) &&
        IsSpawned;

    void OnSlotClicked(int slotIndex)
    {
        if (!slotsPermitidos.Contains(slotIndex)) return;

        if (isRemoveMode)
        {
            if (IsNetworkActive)
                SolicitarRemocaoServerRpc(slotIndex);
            else
                RemoverLocal(slotIndex);
        }
        else AbrirPainelEscolha(slotIndex);
    }

    void ConfirmarEscolha()
    {
        // localId: índice em todosOsPersonagens (personagensDoJogador) — usado para UI
        int localId = todosOsPersonagens.IndexOf(personagemEmVisualizacao);
        int slotConfirmado = slotSendoEditado;

        // Aplica localmente sempre (feedback imediato, independente de RPC)
        AplicarEscolhaLocal(localId, slotConfirmado);

        // Registra escolha do Comandante no cache do servidor via CustomMessage.
        // Funciona mesmo sem NetworkObject spawnado nesta cena.
        // CRÍTICO: usa índice em bibliotecaOriginalPersonagens — mesma lista que GameSetupManager usa no spawn.
        if (slotsPermitidos.Count > 0 && slotConfirmado == slotsPermitidos[0])
            RegisterChoiceWithServer();

        // Sincroniza seleção de UI com outros clientes (requer NetworkObject spawnado)
        if (IsNetworkActive)
            ConfirmarEscolhaServerRpc(localId, slotConfirmado);

        VoltarParaPainelEquipe();

        // TUTORIAL: Ao confirmar o comandante (slot 0) -> pede pra escolher torre
        if (TutorialManager.Instance != null)
        {
            int slotComandante = slotsPermitidos.Count > 0 ? slotsPermitidos[0] : 0;
            if (slotConfirmado == slotComandante)
                TutorialManager.Instance.TriggerTutorial("SELECT_TOWER");
            else if (!GameDataManager.Instance.tutoriaisConcluidos.Contains("EXPLAIN_TRAILS"))
                TutorialManager.Instance.TriggerTutorial("EXPLAIN_TRAILS");
        }
    }

    // --- Paths locais (singleplayer, sem NGO) ---

    void AplicarEscolhaLocal(int id, int slot)
    {
        CharacterBase personagemLocal = todosOsPersonagens[id];
        GameDataManager.Instance.equipeSelecionada[slot] = personagemLocal;
        slotsEquipe[slot].SetPersonagem(personagemLocal);
        AtualizarEstadoBotaoJogar();
        GameDataManager.Instance.SaveGame();
    }

    void RemoverLocal(int slot)
    {
        GameDataManager.Instance.equipeSelecionada[slot] = null;
        slotsEquipe[slot].LimparSlot();
        AtualizarEstadoBotaoJogar();
        GameDataManager.Instance.SaveGame();
    }

    // --- Paths de rede (multiplayer, NGO ativo) ---

    [ServerRpc(RequireOwnership = false)]
    void ConfirmarEscolhaServerRpc(int id, int slot) => ConfirmarEscolhaClientRpc(id, slot);

    [ClientRpc]
    void ConfirmarEscolhaClientRpc(int id, int slot) => AplicarEscolhaLocal(id, slot);

    [ServerRpc(RequireOwnership = false)]
    void RegisterCommanderChoiceServerRpc(int charId, ServerRpcParams rpcParams = default)
        => CharacterChoiceCache.SetClientCharacterIndex(
            rpcParams.Receive.SenderClientId,
            charId,
            "RegisterCommanderChoiceServerRpc");

    // Registra escolha de personagem no servidor sem depender de IsSpawned.
    // Host atualiza o cache diretamente; clientes enviam via CustomMessagingManager.
    private void RegisterChoiceWithServer()
    {
        var bib = GameDataManager.Instance?.bibliotecaOriginalPersonagens;
        int bibliotecaId = (bib != null) ? bib.IndexOf(personagemEmVisualizacao) : -1;
        if (bibliotecaId < 0)
        {
            Debug.LogWarning($"[SelecaoManager] Personagem '{personagemEmVisualizacao?.name}' não encontrado em bibliotecaOriginalPersonagens. Spawn pode usar índice errado.");
            return;
        }

        var nm = NetworkManager.Singleton;
        if (nm == null) return;

        if (LobbyManager.Instance != null)
            LobbyManager.Instance.SelectCharacter(bibliotecaId);

        if (nm.IsServer)
        {
            CharacterChoiceCache.SetHostCharacterIndex(bibliotecaId, "SelecaoManager.RegisterChoiceWithServer");
            Debug.Log($"[SelecaoManager] Host commander choice: index={bibliotecaId} ({personagemEmVisualizacao?.name})");
        }
        else if (nm.IsClient)
        {
            var writer = new FastBufferWriter(sizeof(int), Allocator.Temp);
            writer.WriteValueSafe(bibliotecaId);
            nm.CustomMessagingManager.SendNamedMessage(k_CharChoiceMsg, NetworkManager.ServerClientId, writer);
            writer.Dispose();
            Debug.Log($"[SelecaoManager] Client commander choice sent: index={bibliotecaId} ({personagemEmVisualizacao?.name})");
        }
    }

    private void OnCharacterChoiceReceived(ulong senderId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int charIdx);
        CharacterChoiceCache.SetClientCharacterIndex(senderId, charIdx, "SelecaoManager.CustomMessage");
        Debug.Log($"[SelecaoManager] Commander choice registered: clientId={senderId}, index={charIdx}");

        if (IsServer)
            AtualizarEstadoBotaoJogar();
    }

    [ServerRpc(RequireOwnership = false)]
    void SolicitarRemocaoServerRpc(int slot) => RemoverClientRpc(slot);

    [ClientRpc]
    void RemoverClientRpc(int slot) => RemoverLocal(slot);

    void AtualizarEstadoBotaoJogar()
    {
        if (botaoJogar == null || GameDataManager.Instance == null) return;

        bool localPronto = HasLocalCommanderAndFirstTowerSelected();

        if (GameModeManager.CurrentMode == GameMode.Multiplayer)
        {
            // Habilita o Toggle Pronto se o jogador local escolheu os 2 chars
            if (togglePronto != null)
                togglePronto.interactable = localPronto;

            // Apenas o Host vê o botão de iniciar (ele será habilitado quando todos estiverem prontos)
            var lobby = LobbyManager.Instance?.GetCurrentLobby();
            string myUid = ExoBeasts.Multiplayer.Auth.SessionManager.Instance?.GetUserId() ?? "";
            bool isHost = lobby != null && lobby.hostProductUserId == myUid;
            
            botaoJogar.gameObject.SetActive(isHost);
            VerificarTodosProntos(); // Re-checa o interactable do Host
            
            if (painelJogadoresLobby != null) painelJogadoresLobby.SetActive(true);
        }
        else
        {
            if (togglePronto != null) togglePronto.gameObject.SetActive(false);
            if (painelJogadoresLobby != null) painelJogadoresLobby.SetActive(false);
            
            botaoJogar.gameObject.SetActive(IsNetworkActive ? IsServer : true);
            botaoJogar.interactable = localPronto;
        }
    }

    private bool HasLocalCommanderAndFirstTowerSelected()
    {
        CharacterBase[] equipe = GameDataManager.Instance?.equipeSelecionada;
        if (equipe == null || equipe.Length == 0)
            return false;

        List<int> slotsLocais = slotsPermitidos;
        if ((slotsLocais == null || slotsLocais.Count == 0) && LobbyManager.Instance != null)
        {
            string myUid = ExoBeasts.Multiplayer.Auth.SessionManager.Instance?.GetUserId() ?? "";
            List<LobbyMember> membros = LobbyManager.Instance.GetOrderedMembers();
            int meuIndice = LobbyManager.Instance.GetCanonicalMemberIndex(myUid);
            slotsLocais = PartySlotLayout.GetSlots(membros.Count, meuIndice);
        }

        if (slotsLocais == null || slotsLocais.Count < 2)
            return equipe.Length > 1 && equipe[0] != null && equipe[1] != null;

        int commanderSlot = slotsLocais[0];
        int firstTowerSlot = slotsLocais[1];
        return commanderSlot < equipe.Length &&
               firstTowerSlot < equipe.Length &&
               equipe[commanderSlot] != null &&
               equipe[firstTowerSlot] != null;
    }

    public void AbrirPainelDetalhes(CharacterBase p)
    {
        personagemEmVisualizacao = p;
        painelEscolhaPersonagem.SetActive(false);
        painelDetalhes.SetActive(true);
        imagemDetalhes.sprite = p.characterIcon;
        nomeDetalhes.text = p.name.Replace("(Clone)", "");
        textoStatusPadrao.text = $"Vida: {p.maxHealth}\nDano: {p.damage}\nVelocidade: {p.moveSpeed}";

        // Monta texto com TODAS as habilidades do comandante
        string s = "";
        if (p.passive != null)
            s += $"<b>{p.passive.abilityName} (Passiva):</b>\n{p.passive.description}\n\n";
        if (p.ability1 != null)
            s += $"<b>{p.ability1.abilityName}:</b>\n{p.ability1.description}\n\n";
        if (p.ability2 != null)
            s += $"<b>{p.ability2.abilityName}:</b>\n{p.ability2.description}\n\n";
        if (p.ultimate != null)
            s += $"<b>{p.ultimate.abilityName} (Ultimate):</b>\n{p.ultimate.description}";

        textoHabilidadesComandante.text = s;

        botaoConfirmarEscolha.onClick.RemoveAllListeners();
        botaoConfirmarEscolha.onClick.AddListener(ConfirmarEscolha);
        AtualizarTextoBotoesCaminho(p);

        if (slotSendoEditado == 0) MostrarPainelHabilidades(); else MostrarPainelUpgradesTorre();

        // TUTORIAL: Mostra tutorial de habilidades ou upgrades de torre
        if (TutorialManager.Instance != null)
        {
            int slotComandante = slotsPermitidos.Count > 0 ? slotsPermitidos[0] : 0;
            if (slotSendoEditado == slotComandante)
                TutorialManager.Instance.TriggerTutorial("COMMANDER_SKILLS");
            else
                TutorialManager.Instance.TriggerTutorial("TOWER_UPGRADES");
        }
    }

    void LimparGrid(Transform g) { if (g == null) return; foreach (Transform t in g) Destroy(t.gameObject); }

    void ConfigurarBotoesPrincipais()
    {
        if (togglePronto != null)
        {
            togglePronto.SetIsOnWithoutNotify(false);
            _isReady = false;
            
            togglePronto.onValueChanged.RemoveAllListeners();
            togglePronto.onValueChanged.AddListener((isOn) => {
                _isReady = isOn;
                if (GameModeManager.CurrentMode == GameMode.Multiplayer && LobbyManager.Instance != null)
                {
                    LobbyManager.Instance.SetReady(_isReady);
                    Debug.Log($"[SelecaoManager] Jogador marcou pronto: {_isReady}");
                }
            });
        }

        botaoJogar.onClick.RemoveAllListeners();
        botaoJogar.onClick.AddListener(() => {
            if (GameModeManager.CurrentMode == GameMode.Multiplayer)
            {
                var nm = NetworkManager.Singleton;
                if (nm != null && nm.IsServer)
                {
                    if (!AllConnectedPlayersHaveCommanderChoice())
                    {
                        Debug.LogWarning("[SelecaoManager] Partida bloqueada: ainda existe jogador sem comandante confirmado no cache autoritativo.");
                        AtualizarEstadoBotaoJogar();
                        return;
                    }

                    nm.SceneManager.LoadScene(nomeDaCenaDoJogo, UnityEngine.SceneManagement.LoadSceneMode.Single);
                }
            }
            else
                StartCoroutine(IniciarPartidaSingleplayer());
        });
        botaoVoltarDaEscolha.onClick.AddListener(VoltarParaPainelEquipe);
        botaoVoltarDosDetalhes.onClick.AddListener(VoltarParaPainelEscolha);
        if (botaoRemover != null)
            botaoRemover.onClick.AddListener(ToggleRemoveMode);
    }

    private IEnumerator IniciarPartidaSingleplayer()
    {
        var nm = NetworkManager.Singleton;
        if (nm == null)
        {
            Debug.LogError("[SelecaoManager] NetworkManager não encontrado para StartHost!");
            yield break;
        }

        // Aguarda shutdown completo de sessão anterior (mesmo padrão do LobbyManager)
        if (nm.IsListening || nm.IsHost || nm.IsServer || nm.IsClient)
        {
            Debug.Log("[SelecaoManager] NGO ainda em execução. Aguardando Shutdown antes de StartHost...");
            nm.Shutdown();
            float elapsed = 0f;
            while (nm.IsListening && elapsed < 3f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (nm.IsListening)
            {
                Debug.LogError("[SelecaoManager] Shutdown não completou em 3s. Abortando StartHost.");
                yield break;
            }
        }

        bool started = nm.StartHost();
        if (!started)
        {
            Debug.LogError("[SelecaoManager] StartHost() falhou. Verifique o estado do NetworkManager.");
            yield break;
        }

        nm.SceneManager.LoadScene(nomeDaCenaDoJogo, UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    // Chamado quando um membro do lobby atualiza (inclusive ready)
    private void OnMemberUpdatedCheck(LobbyMember _)
    {
        VerificarTodosProntos();
        AtualizarListaJogadores();
    }

    private void VerificarTodosProntos()
    {
        if (GameModeManager.CurrentMode != GameMode.Multiplayer) return;
        if (LobbyManager.Instance == null) return;

        // Só o host pode iniciar a partida. O botão 'Jogar' fica habilitado apenas quando todos estao prontos.
        var lobby = LobbyManager.Instance.GetCurrentLobby();
        string localUid = ExoBeasts.Multiplayer.Auth.SessionManager.Instance?.GetUserId() ?? "";
        bool isHost = lobby != null
            && !string.IsNullOrEmpty(lobby.hostProductUserId)
            && lobby.hostProductUserId == localUid;
        
        if (!isHost) return;

        var members = LobbyManager.Instance.GetOrderedMembers();
        bool todosProntos = false;
        
        if (members != null && members.Count >= 1) // Em testes pode estar sozinho, ou >1
        {
            todosProntos = members.TrueForAll(m => m.isReady);
        }

        if (botaoJogar != null)
        {
            botaoJogar.interactable = todosProntos && AllConnectedPlayersHaveCommanderChoice();
        }
    }

    private void AtualizarListaJogadores()
    {
        if (containerListaJogadores == null || GameModeManager.CurrentMode != GameMode.Multiplayer) return;
        
        var members = LobbyManager.Instance?.GetOrderedMembers() ?? new List<LobbyMember>();
        string localUid = ExoBeasts.Multiplayer.Auth.SessionManager.Instance?.GetUserId() ?? "";
        var lobby = LobbyManager.Instance?.GetCurrentLobby();
        
        LimparGrid(containerListaJogadores);
        
        int cont = 0;
        foreach (var m in members)
        {
            cont++;
            bool isMe = m.productUserId == localUid;
            bool isHost = lobby != null && lobby.hostProductUserId == m.productUserId;

            string tags = "";
            if (isHost) tags += " <color=#FFD700>[Host]</color>";
            if (m.isReady) tags += " <color=green>✓</color>";
            if (isMe) tags += " <color=yellow>◄ VOCÊ</color>";

            GameObject slot;
            if (prefabSlotJogadorLobby != null) 
            {
                slot = Instantiate(prefabSlotJogadorLobby, containerListaJogadores);
                var ts = slot.GetComponentsInChildren<TextMeshProUGUI>();
                if (ts.Length > 0) ts[0].text = $"{cont}. {m.displayName}{tags}";
            }
            else
            {
                // Fallback dinâmico se n tiver prefab
                slot = new GameObject("PlayerSlot", typeof(RectTransform));
                slot.transform.SetParent(containerListaJogadores, false);
                var txt = slot.AddComponent<TextMeshProUGUI>();
                txt.fontSize = 24;
                txt.alignment = TextAlignmentOptions.MidlineLeft;
                txt.text = $"{cont}. {m.displayName}{tags}";
            }
        }
    }

    void PopularGridDeEscolha()
    {
        foreach (var p in todosOsPersonagens)
        {
            GameObject o = Instantiate(slotEscolhaPrefab, gridEscolhaContainer);
            o.GetComponent<Image>().sprite = p.characterIcon;
            o.GetComponent<Button>().onClick.AddListener(() => AbrirPainelDetalhes(p));
            botoesDeEscolha.Add(p, o.GetComponent<Button>());
        }
    }

    public void AbrirPainelEscolha(int i) 
    { 
        slotSendoEditado = i; 
        
        // Atualiza a disponibilidade e as cores dos botões de seleção
        foreach(var kvp in botoesDeEscolha)
        {
            CharacterBase p = kvp.Key;
            Button btn = kvp.Value;
            
            bool jaSelecionadoEmOutroSlot = false;
            if (GameDataManager.Instance != null && GameDataManager.Instance.equipeSelecionada != null)
            {
                for(int slotIndex = 0; slotIndex < GameDataManager.Instance.equipeSelecionada.Length; slotIndex++)
                {
                    // Ignora o meu próprio slot (pra eu poder ver/re-selecionar o que estou tentando trocar, ou não)
                    if (slotIndex == slotSendoEditado) continue;
                    
                    if (GameDataManager.Instance.equipeSelecionada[slotIndex] == p)
                    {
                        jaSelecionadoEmOutroSlot = true;
                        break;
                    }
                }
            }

            if (jaSelecionadoEmOutroSlot)
            {
                btn.interactable = false;
                btn.image.color = new Color(0.3f, 0.3f, 0.3f, 1f); // Preto/cinza escuro
            }
            else
            {
                btn.interactable = true;
                btn.image.color = Color.white; // Original/limpo
            }
        }

        painelEquipe.SetActive(false); 
        painelEscolhaPersonagem.SetActive(true); 
    }

    public void VoltarParaPainelEquipe() { painelEscolhaPersonagem.SetActive(false); painelDetalhes.SetActive(false); painelEquipe.SetActive(true); }
    public void VoltarParaPainelEscolha() { painelDetalhes.SetActive(false); painelEscolhaPersonagem.SetActive(true); }

    public void IrParaCenaRastros()
    {
        if (personagemEmVisualizacao != null && GameDataManager.Instance != null)
        {
            GameDataManager.Instance.personagemParaRastros = personagemEmVisualizacao;
            UnityEngine.SceneManagement.SceneManager.LoadScene("Rastros");
        }
    }

    public void MostrarPainelHabilidades() 
    { 
        painelHabilidades.SetActive(true); 
        painelUpgradesTorre.SetActive(false); 
        
        // Esconder os botões de caminho quando estiver na aba Comandante
        foreach (var btn in botoesCaminhoTorre)
        {
            if (btn != null) btn.gameObject.SetActive(false);
        }
    }

    public void MostrarPainelUpgradesTorre() 
    { 
        painelHabilidades.SetActive(false); 
        painelUpgradesTorre.SetActive(true); 
        
        // Religa os botões de caminho de acordo com as habilidades
        if (personagemEmVisualizacao != null)
        {
            AtualizarTextoBotoesCaminho(personagemEmVisualizacao);
        }
    }

    void AtualizarTextoBotoesCaminho(CharacterBase p)
    {
        TextMeshProUGUI[] textosCaminho = { textoCaminho1, textoCaminho2, textoCaminho3 };

        for (int i = 0; i < botoesCaminhoTorre.Count; i++)
        {
            int index = i; // Captura local para o listener do botão
            botoesCaminhoTorre[index].onClick.RemoveAllListeners();

            if (index < p.upgradePaths.Count && p.upgradePaths[index] != null)
            {
                var path = p.upgradePaths[index];
                botoesCaminhoTorre[index].GetComponentInChildren<TextMeshProUGUI>().text = path.pathName;
                
                // Exibe os botões apenas se estivermos na aba de torre selecionada
                botoesCaminhoTorre[index].gameObject.SetActive(painelUpgradesTorre.activeSelf);
                
                if (index < textosCaminho.Length && textosCaminho[index] != null)
                {
                    string desc = $"<b>{path.pathName}</b>\n\n";
                    if (path.upgradesInPath != null)
                    {
                        foreach (var upg in path.upgradesInPath)
                        {
                            desc += $"• <b>{upg.upgradeName}</b>: {upg.description} (Custo: <color=#90EE90>{upg.geoditeCost}G</color>)\n";
                        }
                    }
                    textosCaminho[index].text = desc;
                    
                    // Inicializa mostrando APENAS a primeira aba se a aba principal de torres estiver visível
                    textosCaminho[index].gameObject.SetActive(index == 0 && painelUpgradesTorre.activeSelf); 
                }

                // Configura o visual do botão ativado/desativado (o clicado brilha, os outros ficam acinzentados)
                var img = botoesCaminhoTorre[index].GetComponent<Image>();
                if (img != null) img.color = (index == 0) ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);

                // Interliga os botões Dano/Velocidade/Proteção para alternar o texto exibido e a cor
                botoesCaminhoTorre[index].onClick.AddListener(() => {
                    for(int j = 0; j < botoesCaminhoTorre.Count; j++)
                    {
                        if (textosCaminho.Length > j && textosCaminho[j] != null)
                            textosCaminho[j].gameObject.SetActive(j == index);

                        if (botoesCaminhoTorre[j] != null)
                        {
                            var bImg = botoesCaminhoTorre[j].GetComponent<Image>();
                            if (bImg != null)
                            {
                                bImg.color = (j == index) ? Color.white : new Color(0.5f, 0.5f, 0.5f, 1f);
                            }
                        }
                    }
                });
            }
            else
            {
                botoesCaminhoTorre[index].gameObject.SetActive(false);
                if (index < textosCaminho.Length && textosCaminho[index] != null)
                {
                    textosCaminho[index].text = "";
                    textosCaminho[index].gameObject.SetActive(false);
                }
            }
        }
    }
    
    void ToggleRemoveMode() { isRemoveMode = !isRemoveMode; if (botaoRemover != null) botaoRemover.image.color = isRemoveMode ? corModoRemover : corOriginalBotaoRemover; }

    private bool AllConnectedPlayersHaveCommanderChoice()
    {
        if (GameModeManager.CurrentMode != GameMode.Multiplayer)
            return true;

        var nm = NetworkManager.Singleton;
        if (nm == null || !nm.IsServer)
            return false;

        foreach (ulong clientId in nm.ConnectedClientsIds)
        {
            if (!CharacterChoiceCache.HasChoice(clientId))
                return false;
        }

        return true;
    }
}
