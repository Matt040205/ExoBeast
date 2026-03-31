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
        CharacterBase modelo = todosOsPersonagens[id];
        if (GameDataManager.Instance.equipeSelecionada[slot] != null)
            Destroy(GameDataManager.Instance.equipeSelecionada[slot]);

        CharacterBase novaInstancia = Instantiate(modelo);
        GameDataManager.Instance.AplicarDadosCarregados(novaInstancia);
        GameDataManager.Instance.equipeSelecionada[slot] = novaInstancia;
        slotsEquipe[slot].SetPersonagem(novaInstancia);
        AtualizarEstadoBotaoJogar();
        GameDataManager.Instance.SaveGame();
    }

    void RemoverLocal(int slot)
    {
        if (GameDataManager.Instance.equipeSelecionada[slot] != null)
            Destroy(GameDataManager.Instance.equipeSelecionada[slot]);

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
        nomeDetalhes.text = p.name;
        textoStatusPadrao.text = $"Vida: {p.maxHealth}\nDano: {p.damage}\nVel: {p.moveSpeed}";

        string s = "";
        if (p.passive != null) s += $"<b>{p.passive.abilityName}:</b> {p.passive.description}\n\n";
        if (p.ability1 != null) s += $"<b>{p.ability1.abilityName}:</b> {p.ability1.description}\n\n";
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

    public void AbrirPainelEscolha(int i) { slotSendoEditado = i; painelEquipe.SetActive(false); painelEscolhaPersonagem.SetActive(true); }
    public void VoltarParaPainelEquipe() { painelEscolhaPersonagem.SetActive(false); painelDetalhes.SetActive(false); painelEquipe.SetActive(true); }
    public void VoltarParaPainelEscolha() { painelDetalhes.SetActive(false); painelEscolhaPersonagem.SetActive(true); }

    void AtualizarTextoBotoesCaminho(CharacterBase p)
    {
        TextMeshProUGUI[] textosCaminho = { textoCaminho1, textoCaminho2, textoCaminho3 };

        for (int i = 0; i < botoesCaminhoTorre.Count; i++)
        {
            if (i < p.upgradePaths.Count)
            {
                botoesCaminhoTorre[i].GetComponentInChildren<TextMeshProUGUI>().text = p.upgradePaths[i].pathName;
                botoesCaminhoTorre[i].gameObject.SetActive(true);
                if (i < textosCaminho.Length && textosCaminho[i] != null)
                    textosCaminho[i].text = p.upgradePaths[i].pathName;
            }
            else
            {
                botoesCaminhoTorre[i].gameObject.SetActive(false);
                if (i < textosCaminho.Length && textosCaminho[i] != null)
                    textosCaminho[i].text = "";
            }
        }
    }

    public void MostrarPainelHabilidades() { painelHabilidades.SetActive(true); painelUpgradesTorre.SetActive(false); }
    public void MostrarPainelUpgradesTorre() { painelHabilidades.SetActive(false); painelUpgradesTorre.SetActive(true); }
    void ToggleRemoveMode() { isRemoveMode = !isRemoveMode; if (botaoRemover != null) botaoRemover.image.color = isRemoveMode ? corModoRemover : corOriginalBotaoRemover; }
}