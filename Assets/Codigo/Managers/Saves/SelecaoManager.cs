using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Unity.Netcode;
using DG.Tweening;
using ExoBeasts.Managers;
using ExoBeasts.Multiplayer.Lobby;

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
    public string nomeDaCenaDoJogo = "CenaMapaTeste";

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

    private int slotInicialPermitido = 0;
    private int slotFinalPermitido = 7;

    private void Awake() => Instance = this;

    void Start()
    {
        if (botaoRemover != null) corOriginalBotaoRemover = botaoRemover.image.color;
        StartCoroutine(SetupScene());
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
    }

    public void CalcularLimitesDeSlots()
    {
        if (NetworkManager.Singleton == null || (!NetworkManager.Singleton.IsClient && !NetworkManager.Singleton.IsServer))
        {
            slotInicialPermitido = 0; slotFinalPermitido = 7;
            return;
        }

        var membros = ExoBeasts.Multiplayer.Lobby.LobbyManager.Instance.GetMembers();
        string meuId = ExoBeasts.Multiplayer.Auth.SessionManager.Instance.GetUserId();
        int meuIndice = membros.FindIndex(m => m.productUserId == meuId);
        int total = membros.Count;

        if (meuIndice == -1) return;

        if (total == 2) { slotInicialPermitido = meuIndice * 4; slotFinalPermitido = slotInicialPermitido + 3; }
        else if (total == 3)
        {
            if (meuIndice == 0) { slotInicialPermitido = 0; slotFinalPermitido = 3; }
            else if (meuIndice == 1) { slotInicialPermitido = 4; slotFinalPermitido = 5; }
            else { slotInicialPermitido = 6; slotFinalPermitido = 7; }
        }
        else if (total == 4) { slotInicialPermitido = meuIndice * 2; slotFinalPermitido = slotInicialPermitido + 1; }
    }

    void CriarGridEquipe()
    {
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
        if (slotIndex < slotInicialPermitido || slotIndex > slotFinalPermitido) return;

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
        int id = todosOsPersonagens.IndexOf(personagemEmVisualizacao);

        if (IsNetworkActive)
            ConfirmarEscolhaServerRpc(id, slotSendoEditado);
        else
            AplicarEscolhaLocal(id, slotSendoEditado);

        VoltarParaPainelEquipe();
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
    void SolicitarRemocaoServerRpc(int slot) => RemoverClientRpc(slot);

    [ClientRpc]
    void RemoverClientRpc(int slot) => RemoverLocal(slot);

    void AtualizarEstadoBotaoJogar()
    {
        if (botaoJogar == null || GameDataManager.Instance == null) return;

        if (IsNetworkActive)
            botaoJogar.gameObject.SetActive(IsServer);
        else
            botaoJogar.gameObject.SetActive(true);

        bool pronto = GameDataManager.Instance.equipeSelecionada[0] != null &&
                      GameDataManager.Instance.equipeSelecionada[1] != null;

        botaoJogar.interactable = pronto;
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
    }

    void LimparGrid(Transform g) { if (g == null) return; foreach (Transform t in g) Destroy(t.gameObject); }

    void ConfigurarBotoesPrincipais()
    {
        botaoJogar.onClick.RemoveAllListeners();
        botaoJogar.onClick.AddListener(() =>
        {
            if (GameModeManager.CurrentMode == GameMode.Multiplayer)
            {
                if (LobbyManager.Instance != null)
                    LobbyManager.Instance.StartMatch();
                else
                    Debug.LogError("[SelecaoManager] LobbyManager nao encontrado para StartMatch!");
            }
            else
            {
                if (NetworkManager.Singleton != null)
                {
                    NetworkManager.Singleton.StartHost();
                    NetworkManager.Singleton.SceneManager.LoadScene(
                        nomeDaCenaDoJogo, UnityEngine.SceneManagement.LoadSceneMode.Single);
                }
                else
                {
                    Debug.LogError("[SelecaoManager] NetworkManager não encontrado para StartHost!");
                }
            }
        });
        botaoVoltarDaEscolha.onClick.AddListener(VoltarParaPainelEquipe);
        botaoVoltarDosDetalhes.onClick.AddListener(VoltarParaPainelEscolha);
        if (botaoRemover != null)
            botaoRemover.onClick.AddListener(ToggleRemoveMode);
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
}