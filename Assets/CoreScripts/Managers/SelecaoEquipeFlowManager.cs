using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;
using ExoBeasts.Managers;
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Multiplayer.Lobby;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using Unity.Collections;

/// <summary>
/// ── SelecaoEquipeFlowManager ──────────────────────────────
/// Gerenciador completo do fluxo da CenaSeleção.
///
///  ▸ Exibe os Sprites dos ícones de Habilidades (Passiva, Hab1, Hab2, Ultimate) ao clicar no Comandante.
///  ▸ Exibe os Sprites dos Upgrades nos 5 Níveis de cada Caminho da Torre.
/// ──────────────────────────────────────────────────────────
/// </summary>
public class SelecaoEquipeFlowManager : MonoBehaviour
{
    private const string k_CharChoiceMsg = "ExoBeasts.CharacterChoice";

    // ── CONFIGURAÇÃO DE PERSONAGENS ─────────────────────
    [Header(" Configuração da Lista de Personagens")]
    [Tooltip("Lista de personagens disponíveis. Se estiver vazia, usará automaticamente GameDataManager.Instance.personagensDoJogador")]
    public List<CharacterBase> personagensDisponiveis = new List<CharacterBase>();
    
    [Tooltip("Prefab do Card de Seleção do Personagem (opcional se o Grid na cena já tiver os quadros criados)")]
    public GameObject cardPrefab;

    [Tooltip("Prefab do Slot de Torre Equipada no Canvas de Torres (opcional se os slots já existirem)")]
    public GameObject slotTorreEquipadaPrefab;

    [Header(" Preview 3D & Info Global no Pedestal")]
    [Tooltip("Transform no centro da área de preview (pedestal) onde o modelo 3D do Comandante ou Torre é instanciado")]
    public Transform spawnPontoPreview;

    [Tooltip("Transform opcional para onde o modelo 3D no pedestal deve olhar (se vazio, usará a rotação do próprio Spawn Ponto Preview)")]
    public Transform olharParaAlvo;

    [Tooltip("Capsule placeholder para ocultar quando o modelo 3D real for instanciado")]
    public GameObject capsulePlaceholder;

    [Tooltip("Texto do Nome do Personagem/Torre em foco na cena")]
    public TextMeshProUGUI nomePersonagem;

    [Tooltip("Ícone do Personagem/Torre em foco na cena")]
    public Image iconePersonagem;

    // ── STRUCT PARA NÍVEIS DE CAMINHO ──────────────────
    [System.Serializable]
    public class CaminhoTorreUIItem
    {
        public GameObject containerCaminho; // GameObject do Caminho (ex: Caminho1)
        public TextMeshProUGUI textoNomeCaminho;
        public Button botaoSelecionarCaminho;

        [Tooltip("5 GameObjects/Ícones dos 5 Níveis de Upgrade (do Nível 1 à esquerda ao Nível 5 à direita)")]
        public GameObject[] nivelIcons = new GameObject[5];
    }

    // ── ESTÁGIO 1: COMANDANTE ───────────────────────────
    [System.Serializable]
    public class EstagioComandanteData
    {
        [Header("Câmera e Canvas")]
        public CinemachineCamera cmPersonagem;
        public GameObject canvasPersonagem;

        [Header("Layouts / Abas UI")]
        public Transform abaEscolhaPersonagem;
        public GameObject abaTipoPersonagem;
        public GameObject abaDetalhesComandante;
        public GameObject abaDetalhesTorre;

        [Header("Botões do TipoPersonagem")]
        public Button botaoSubAbaComandante;
        public Button botaoSubAbaTorre;

        [Header("Aba Única de Hover (Tooltip) do Canvas Comandante")]
        public GameObject abaHoverTooltip;
        public TextMeshProUGUI textoHoverNome;
        public TextMeshProUGUI textoHoverConteudo;

        [Header("Ícones / Containers de Habilidades no Canvas")]
        public GameObject iconPassiva;
        public GameObject iconHabilidade1;
        public GameObject iconHabilidade2;
        public GameObject iconUltimate;

        [Header("3 Caminhos no Kit da Torre (com 5 níveis cada)")]
        public CaminhoTorreUIItem caminho1;
        public CaminhoTorreUIItem caminho2;
        public CaminhoTorreUIItem caminho3;

        [Header("Ações & Navegação")]
        [Tooltip("Botão Confirmar: Seleciona e fixa o Comandante na equipe (sem mudar de etapa)")]
        public Button botaoConfirmarComandante;
        [Tooltip("Botão Próxima Etapa: Transiciona a câmera e o canvas para a Seleção de Torres")]
        public Button botaoProximaEtapa;

        [Header("Pop-Up de Confirmação (Opcional)")]
        public GameObject popupConfirmacao;
        public Button botaoPopupConfirmar;
        public Button botaoPopupCancelar;
    }

    [Header(" ESTÁGIO 1: SELEÇÃO DE COMANDANTE")]
    public EstagioComandanteData estagioComandante;

    // ── ESTÁGIO 2: TORRES ───────────────────────────────
    [System.Serializable]
    public class EstagioTorresData
    {
        [Header("Câmera e Canvas")]
        public CinemachineCamera cmTorres;
        public GameObject canvasTorres;

        [Header("Layouts / Abas UI")]
        public Transform abaEscolhaPersonagem;
        public GameObject abaCaminhoTorre;

        [Header("Containers das Torres Equipadas (Dividido em 2 partes)")]
        public Transform abaTorresEquipadas1_4;
        public Transform abaTorresEquipadas5_7;

        [Header("Aba Única de Hover (Tooltip) do Canvas Torres")]
        public GameObject abaHoverTooltip;
        public TextMeshProUGUI textoHoverNome;
        public TextMeshProUGUI textoHoverConteudo;

        [Header("3 Caminhos de Upgrade (com 5 níveis cada)")]
        public CaminhoTorreUIItem caminho1;
        public CaminhoTorreUIItem caminho2;
        public CaminhoTorreUIItem caminho3;

        [Header("Ações & Navegação")]
        [Tooltip("Botão Confirmar/Adicionar Torre: Coloca a torre em foco numa das abas de equipadas (1-4 ou 5-7)")]
        public Button botaoAdicionarTorre;
        public TextMeshProUGUI textoNomeTorreEmFoco;
        [Tooltip("Botão Próxima Etapa: Transiciona a câmera e o canvas para a Confirmação Final")]
        public Button botaoProximaEtapa;
    }

    [Header(" ESTÁGIO 2: SELEÇÃO DE TORRES")]
    public EstagioTorresData estagioTorres;

    // ── ESTÁGIO 3: CONFIRMAÇÃO FINAL ───────────────────
    [System.Serializable]
    public class EstagioConfirmacaoFinalData
    {
        [Header("Câmera e Canvas")]
        public CinemachineCamera cmConfirmacao;
        public GameObject canvasConfirmacao;

        [Header("Preview 3D da Equipe Final")]
        public Transform spawnComandanteFinal;
        public Transform[] spawnTorresFinal;

        [Header("Containers da Equipe Final (Dividido em 2 partes)")]
        public Transform abaResumoEquipe1_4;
        public Transform abaResumoEquipe5_7;

        [Header("Botão Iniciar Partida")]
        public Button botaoIniciarPartida;

        [Header("Cena do Jogo")]
        public string nomeDaCenaDoJogo = "CenaMapaNOVO";
    }

    [Header(" ESTÁGIO 3: CONFIRMAÇÃO FINAL DA EQUIPE")]
    public EstagioConfirmacaoFinalData estagioConfirmacao;

    [System.Serializable]
    public class MultiplayerSelectionData
    {
        [Header("Painel de Jogadores")]
        public GameObject painelOutrosJogadores;
        public Transform containerListaJogadores;
        public TextMeshProUGUI textoStatus;
        public Button botaoPronto;
        public TextMeshProUGUI textoBotaoPronto;
    }

    [Header(" MULTIPLAYER: STATUS E PRONTO")]
    public MultiplayerSelectionData multiplayerUi = new MultiplayerSelectionData();

    [Header("Cores dos Jogadores")]
    public Color[] coresPorJogador = new Color[] {
        new Color(0.9764706f, 0.5921569f, 0.9803922f, 1f),
        new Color(0.5921569f, 0.8352941f, 0.9803922f, 1f),
        new Color(0.6980392f, 0.9803922f, 0.5921569f, 1f),
        new Color(0.9803922f, 0.9098039f, 0.5921569f, 1f)
    };

    // ── ESTADO INTERNO DE SELEÇÃO ───────────────────────
    private CharacterBase _comandanteSelecionado;
    private CharacterBase _personagemVisualizacaoComandante;
    private CharacterBase _torreVisualizacaoTorres;
    private List<CharacterBase> _torresEquipadas = new List<CharacterBase>();
    private GameObject _modeloPreviewAtual;
    private List<GameObject> _modelosFinalInstanciados = new List<GameObject>();
    private int _maxTorres = 7;
    private readonly List<int> _slotsPermitidos = new List<int> { 0, 1, 2, 3, 4, 5, 6, 7 };
    private bool _isReady;
    private bool _multiplayerCallbacksRegistered;
    private bool _selectionSceneInitialized;

    // ────────────────────────────────────────────────────
    void Start()
    {
        InicializarSelecaoMultiplayer();
        IniciarFluxo();
    }

    public void IniciarFluxo()
    {
        ExibirEstagioComandante();

        OcultarEstagioTorres();
        OcultarEstagioConfirmacao();

        ConfigurarBotoesEstagio1();
        ConfigurarBotoesEstagio2();
        ConfigurarBotoesEstagio3();
        ConfigurarPainelMultiplayer();
        AtualizarEstadoProntoEInicio();

        StartCoroutine(CarregarPersonagensEPopularGrids());
    }

    private IEnumerator CarregarPersonagensEPopularGrids()
    {
        float timer = 0f;
        while (GameDataManager.Instance == null && timer < 1f)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        if (personagensDisponiveis == null || personagensDisponiveis.Count == 0)
        {
            if (GameDataManager.Instance != null && GameDataManager.Instance.personagensDoJogador != null && GameDataManager.Instance.personagensDoJogador.Count > 0)
            {
                personagensDisponiveis = new List<CharacterBase>(GameDataManager.Instance.personagensDoJogador);
            }
            else
            {
                CharacterBase[] carregados = Resources.FindObjectsOfTypeAll<CharacterBase>();
                if (carregados != null && carregados.Length > 0)
                {
                    personagensDisponiveis = new List<CharacterBase>();
                    foreach (var c in carregados)
                    {
                        if (c != null && !c.name.Contains("(Clone)"))
                            personagensDisponiveis.Add(c);
                    }
                }
            }
        }

        PopularGridComandante();
    }

    // ────────────────────────────────────────────────────
    // ESTÁGIO 1: SELEÇÃO DE COMANDANTE
    // ────────────────────────────────────────────────────
    private void ExibirEstagioComandante()
    {
        if (estagioComandante.cmPersonagem != null) estagioComandante.cmPersonagem.gameObject.SetActive(true);
        SetCanvasPersonagemActive(true);

        if (capsulePlaceholder != null) capsulePlaceholder.SetActive(true);
        if (estagioComandante.abaTipoPersonagem != null) estagioComandante.abaTipoPersonagem.SetActive(false);
        if (estagioComandante.abaDetalhesComandante != null) estagioComandante.abaDetalhesComandante.SetActive(false);
        if (estagioComandante.abaDetalhesTorre != null) estagioComandante.abaDetalhesTorre.SetActive(false);
        if (estagioComandante.popupConfirmacao != null) estagioComandante.popupConfirmacao.SetActive(false);
        if (estagioComandante.abaHoverTooltip != null) estagioComandante.abaHoverTooltip.SetActive(false);
    }

    private void ConfigurarBotoesEstagio1()
    {
        if (estagioComandante.botaoSubAbaComandante != null)
            estagioComandante.botaoSubAbaComandante.onClick.AddListener(AbrirSubAbaComandante);

        if (estagioComandante.botaoSubAbaTorre != null)
            estagioComandante.botaoSubAbaTorre.onClick.AddListener(AbrirSubAbaTorre);

        if (estagioComandante.botaoConfirmarComandante != null)
            estagioComandante.botaoConfirmarComandante.onClick.AddListener(SolicitarConfirmacaoComandante);

        if (estagioComandante.botaoProximaEtapa != null)
            estagioComandante.botaoProximaEtapa.onClick.AddListener(TransicionarParaEstagioTorres);

        if (estagioComandante.botaoPopupConfirmar != null)
            estagioComandante.botaoPopupConfirmar.onClick.AddListener(ConfirmarComandante);

        if (estagioComandante.botaoPopupCancelar != null)
            estagioComandante.botaoPopupCancelar.onClick.AddListener(() => {
                if (estagioComandante.popupConfirmacao != null) estagioComandante.popupConfirmacao.SetActive(false);
            });
    }

    private void PopularGridComandante()
    {
        if (estagioComandante.abaEscolhaPersonagem == null) return;
        Transform container = estagioComandante.abaEscolhaPersonagem;

        int childCount = container.childCount;
        if (childCount > 0 && cardPrefab == null)
        {
            for (int i = 0; i < childCount; i++)
            {
                Transform filho = container.GetChild(i);
                if (i < personagensDisponiveis.Count)
                {
                    CharacterBase p = personagensDisponiveis[i];
                    filho.gameObject.SetActive(true);
                    DefinirImagemNoCard(filho.gameObject, p.characterIcon);

                    Button btn = filho.GetComponent<Button>();
                    if (btn == null) btn = filho.GetComponentInChildren<Button>(true);
                    if (btn != null)
                    {
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => AoSelecionarPersonagemComandante(p));
                    }
                }
                else
                {
                    filho.gameObject.SetActive(false);
                }
            }
            return;
        }

        if (cardPrefab != null)
        {
            LimparContainer(container);
            foreach (var p in personagensDisponiveis)
            {
                if (p == null) continue;
                CharacterBase capturedP = p;

                GameObject card = Instantiate(cardPrefab, container);
                DefinirImagemNoCard(card, capturedP.characterIcon);

                Button btn = card.GetComponent<Button>();
                if (btn == null) btn = card.GetComponentInChildren<Button>(true);
                if (btn != null)
                {
                    btn.onClick.RemoveAllListeners();
                    btn.onClick.AddListener(() => AoSelecionarPersonagemComandante(capturedP));
                }
            }
        }
    }

    private void AoSelecionarPersonagemComandante(CharacterBase p)
    {
        _personagemVisualizacaoComandante = p;

        AtualizarNomeEIconeGlobal(p);

        if (estagioComandante.abaTipoPersonagem != null)
            estagioComandante.abaTipoPersonagem.SetActive(true);

        ConfigurarHoverHabilidades(p);
        ConfigurarHoverCaminhosTorreItem(estagioComandante.caminho1, 0, p.upgradePaths, estagioComandante.abaHoverTooltip, estagioComandante.textoHoverNome, estagioComandante.textoHoverConteudo);
        ConfigurarHoverCaminhosTorreItem(estagioComandante.caminho2, 1, p.upgradePaths, estagioComandante.abaHoverTooltip, estagioComandante.textoHoverNome, estagioComandante.textoHoverConteudo);
        ConfigurarHoverCaminhosTorreItem(estagioComandante.caminho3, 2, p.upgradePaths, estagioComandante.abaHoverTooltip, estagioComandante.textoHoverNome, estagioComandante.textoHoverConteudo);

        AbrirSubAbaComandante();

        AtualizarPreview3D(p.commanderPrefab, spawnPontoPreview, capsulePlaceholder);
    }

    private void AbrirSubAbaComandante()
    {
        if (estagioComandante.abaDetalhesTorre != null) estagioComandante.abaDetalhesTorre.SetActive(false);
        if (estagioComandante.abaDetalhesComandante != null) estagioComandante.abaDetalhesComandante.SetActive(true);
    }

    private void AbrirSubAbaTorre()
    {
        if (estagioComandante.abaDetalhesComandante != null) estagioComandante.abaDetalhesComandante.SetActive(false);
        if (estagioComandante.abaDetalhesTorre != null) estagioComandante.abaDetalhesTorre.SetActive(true);
    }

    private void SolicitarConfirmacaoComandante()
    {
        if (_personagemVisualizacaoComandante == null) return;

        if (estagioComandante.popupConfirmacao != null)
            estagioComandante.popupConfirmacao.SetActive(true);
        else
            ConfirmarComandante();
    }

    private void ConfirmarComandante()
    {
        if (_personagemVisualizacaoComandante == null) return;

        _comandanteSelecionado = _personagemVisualizacaoComandante;

        if (estagioComandante.popupConfirmacao != null)
            estagioComandante.popupConfirmacao.SetActive(false);

        SalvarSelecaoLocalNoGameData();

        RegistrarEscolhaComandanteRede(_comandanteSelecionado);
        AtualizarEstadoProntoEInicio();

        Debug.Log($"[SelecaoEquipeFlowManager] Comandante confirmado: {_comandanteSelecionado.name}.");
    }

    private void OcultarEstagioComandante()
    {
        if (estagioComandante.cmPersonagem != null) estagioComandante.cmPersonagem.gameObject.SetActive(false);
        SetCanvasPersonagemActive(false);
    }

    // ────────────────────────────────────────────────────
    // ESTÁGIO 2: SELEÇÃO DE TORRES
    // ────────────────────────────────────────────────────
    private void TransicionarParaEstagioTorres()
    {
        OcultarEstagioComandante();
        ExibirEstagioTorres();
        PopularGridTorres();
        AtualizarAbaTorresEquipadas();
        AtualizarEstadoProntoEInicio();
    }

    private void ExibirEstagioTorres()
    {
        if (estagioTorres.cmTorres != null) estagioTorres.cmTorres.gameObject.SetActive(true);
        if (estagioTorres.canvasTorres != null) estagioTorres.canvasTorres.SetActive(true);

        if (estagioTorres.abaCaminhoTorre != null) estagioTorres.abaCaminhoTorre.SetActive(false);
        if (estagioTorres.abaHoverTooltip != null) estagioTorres.abaHoverTooltip.SetActive(false);
    }

    private void OcultarEstagioTorres()
    {
        if (estagioTorres.cmTorres != null) estagioTorres.cmTorres.gameObject.SetActive(false);
        if (estagioTorres.canvasTorres != null) estagioTorres.canvasTorres.SetActive(false);
    }

    private void ConfigurarBotoesEstagio2()
    {
        if (estagioTorres.botaoAdicionarTorre != null)
            estagioTorres.botaoAdicionarTorre.onClick.AddListener(EquiparTorreEmFoco);

        if (estagioTorres.botaoProximaEtapa != null)
            estagioTorres.botaoProximaEtapa.onClick.AddListener(TransicionarParaEstagioConfirmacao);
    }

    private void PopularGridTorres()
    {
        if (estagioTorres.abaEscolhaPersonagem == null) return;
        Transform container = estagioTorres.abaEscolhaPersonagem;

        int childCount = container.childCount;
        if (childCount > 0 && cardPrefab == null)
        {
            for (int i = 0; i < childCount; i++)
            {
                Transform filho = container.GetChild(i);
                if (i < personagensDisponiveis.Count)
                {
                    CharacterBase p = personagensDisponiveis[i];
                    filho.gameObject.SetActive(true);
                    DefinirImagemNoCard(filho.gameObject, p.characterIcon);

                    Button btn = filho.GetComponent<Button>();
                    if (btn == null) btn = filho.GetComponentInChildren<Button>(true);

                    bool isComandante = (_comandanteSelecionado != null && p.name == _comandanteSelecionado.name);
                    if (isComandante)
                    {
                        Image img = filho.GetComponentInChildren<Image>(true);
                        if (img != null) img.color = new Color(0.3f, 0.3f, 0.3f, 0.7f);
                        if (btn != null) btn.interactable = false;
                    }
                    else
                    {
                        if (btn != null)
                        {
                            btn.interactable = true;
                            btn.onClick.RemoveAllListeners();
                            btn.onClick.AddListener(() => AoSelecionarPersonagemTorre(p));
                        }
                    }
                }
                else
                {
                    filho.gameObject.SetActive(false);
                }
            }
            return;
        }

        if (cardPrefab != null)
        {
            LimparContainer(container);
            foreach (var p in personagensDisponiveis)
            {
                if (p == null) continue;
                CharacterBase capturedP = p;

                GameObject card = Instantiate(cardPrefab, container);
                DefinirImagemNoCard(card, capturedP.characterIcon);

                Button btn = card.GetComponent<Button>();
                if (btn == null) btn = card.GetComponentInChildren<Button>(true);

                bool isComandante = (_comandanteSelecionado != null && capturedP.name == _comandanteSelecionado.name);
                if (isComandante)
                {
                    Image img = card.GetComponentInChildren<Image>(true);
                    if (img != null) img.color = new Color(0.3f, 0.3f, 0.3f, 0.7f);
                    if (btn != null) btn.interactable = false;
                }
                else
                {
                    if (btn != null)
                    {
                        btn.interactable = true;
                        btn.onClick.RemoveAllListeners();
                        btn.onClick.AddListener(() => AoSelecionarPersonagemTorre(capturedP));
                    }
                }
            }
        }
    }

    private void AoSelecionarPersonagemTorre(CharacterBase p)
    {
        _torreVisualizacaoTorres = p;

        if (estagioTorres.abaCaminhoTorre != null)
            estagioTorres.abaCaminhoTorre.SetActive(true);

        if (estagioTorres.textoNomeTorreEmFoco != null)
            estagioTorres.textoNomeTorreEmFoco.text = p.name.Replace("(Clone)", "");

        AtualizarNomeEIconeGlobal(p);

        ConfigurarHoverCaminhosTorreItem(estagioTorres.caminho1, 0, p.upgradePaths, estagioTorres.abaHoverTooltip, estagioTorres.textoHoverNome, estagioTorres.textoHoverConteudo);
        ConfigurarHoverCaminhosTorreItem(estagioTorres.caminho2, 1, p.upgradePaths, estagioTorres.abaHoverTooltip, estagioTorres.textoHoverNome, estagioTorres.textoHoverConteudo);
        ConfigurarHoverCaminhosTorreItem(estagioTorres.caminho3, 2, p.upgradePaths, estagioTorres.abaHoverTooltip, estagioTorres.textoHoverNome, estagioTorres.textoHoverConteudo);

        GameObject prefabTorre = (p.towerPrefab != null) ? p.towerPrefab : p.commanderPrefab;
        AtualizarPreview3D(prefabTorre, spawnPontoPreview, capsulePlaceholder);
    }

    private void EquiparTorreEmFoco()
    {
        if (_torreVisualizacaoTorres == null) return;

        int maxTorresPermitidas = GetMaxTorresPermitidas();
        if (_torresEquipadas.Count < maxTorresPermitidas)
        {
            _torresEquipadas.Add(_torreVisualizacaoTorres);
            SalvarSelecaoLocalNoGameData();
            AtualizarAbaTorresEquipadas();
            AtualizarEstadoProntoEInicio();
            Debug.Log($"[SelecaoEquipeFlowManager] Torre equipada: {_torreVisualizacaoTorres.name} (Total equipadas: {_torresEquipadas.Count}/{maxTorresPermitidas})");
        }
        else
        {
            Debug.LogWarning($"[SelecaoEquipeFlowManager] Limite maximo de {maxTorresPermitidas} torres atingido.");
        }
    }

    private void AtualizarAbaTorresEquipadas()
    {
        AtualizarContainerSlotsTorres(estagioTorres.abaTorresEquipadas1_4, 0, 4, true);
        AtualizarContainerSlotsTorres(estagioTorres.abaTorresEquipadas5_7, 4, 3, true);
    }

    // ────────────────────────────────────────────────────
    // CONFIGURAÇÃO DO HOVER ÚNICO E ÍCONES DE HABILIDADES
    // ────────────────────────────────────────────────────
    private void ConfigurarHoverHabilidades(CharacterBase p)
    {
        // 1. Aplica Sprites dos Ícones Visuais de Habilidade na UI
        DefinirImagemNoIconeHabilidade(estagioComandante.iconPassiva, p.passive?.icon);
        DefinirImagemNoIconeHabilidade(estagioComandante.iconHabilidade1, p.ability1?.icon);
        DefinirImagemNoIconeHabilidade(estagioComandante.iconHabilidade2, p.ability2?.icon);
        DefinirImagemNoIconeHabilidade(estagioComandante.iconUltimate, p.ultimate?.icon);

        // 2. Configurar Triggers de Hover Tooltip
        ConfigurarTriggerHover(estagioComandante.iconPassiva, p.passive?.abilityName ?? "Passiva", p.passive?.description ?? "", estagioComandante.abaHoverTooltip, estagioComandante.textoHoverNome, estagioComandante.textoHoverConteudo);
        ConfigurarTriggerHover(estagioComandante.iconHabilidade1, p.ability1?.abilityName ?? "Habilidade 1", p.ability1?.description ?? "", estagioComandante.abaHoverTooltip, estagioComandante.textoHoverNome, estagioComandante.textoHoverConteudo);
        ConfigurarTriggerHover(estagioComandante.iconHabilidade2, p.ability2?.abilityName ?? "Habilidade 2", p.ability2?.description ?? "", estagioComandante.abaHoverTooltip, estagioComandante.textoHoverNome, estagioComandante.textoHoverConteudo);
        ConfigurarTriggerHover(estagioComandante.iconUltimate, p.ultimate?.abilityName ?? "Ultimate", p.ultimate?.description ?? "", estagioComandante.abaHoverTooltip, estagioComandante.textoHoverNome, estagioComandante.textoHoverConteudo);
    }

    private void DefinirImagemNoIconeHabilidade(GameObject containerIcone, Sprite spriteHabilidade)
    {
        if (containerIcone == null) return;

        Image img = containerIcone.GetComponent<Image>();
        if (img == null) img = containerIcone.GetComponentInChildren<Image>(true);

        if (img != null)
        {
            if (spriteHabilidade != null)
            {
                img.sprite = spriteHabilidade;
                img.enabled = true;
                img.color = Color.white;
            }
            else
            {
                img.enabled = (img.sprite != null);
            }
        }
    }

    private void ConfigurarHoverCaminhosTorreItem(CaminhoTorreUIItem item, int pathIndex, List<UpgradePath> paths, GameObject abaTooltip, TextMeshProUGUI textoNome, TextMeshProUGUI textoConteudo)
    {
        if (item == null) return;

        bool temPath = (paths != null && pathIndex < paths.Count && paths[pathIndex] != null);
        if (item.containerCaminho != null)
            item.containerCaminho.SetActive(temPath);

        if (!temPath) return;

        var path = paths[pathIndex];
        if (item.textoNomeCaminho != null) item.textoNomeCaminho.text = path.pathName;

        if (item.nivelIcons != null && item.nivelIcons.Length > 0 && path.upgradesInPath != null)
        {
            for (int i = 0; i < item.nivelIcons.Length; i++)
            {
                int nivelIndex = i;
                GameObject iconObj = item.nivelIcons[i];
                if (iconObj == null) continue;

                bool temUpgradeNivel = (nivelIndex < path.upgradesInPath.Count && path.upgradesInPath[nivelIndex] != null);
                var up = temUpgradeNivel ? path.upgradesInPath[nivelIndex] : null;

                string tituloNivel = $"{path.pathName} - Nível {nivelIndex + 1}";
                if (up != null && !string.IsNullOrEmpty(up.upgradeName)) tituloNivel += $" ({up.upgradeName})";

                string descNivel = (up != null && !string.IsNullOrEmpty(up.description)) 
                    ? up.description 
                    : $"Upgrade de Nível {nivelIndex + 1} para o caminho {path.pathName}.";

                ConfigurarTriggerHover(iconObj, tituloNivel, descNivel, abaTooltip, textoNome, textoConteudo);
            }
        }
        else
        {
            ConfigurarTriggerHover(item.containerCaminho, $"Caminho {pathIndex + 1}: {path.pathName}", "Caminho de upgrade da torre.", abaTooltip, textoNome, textoConteudo);
        }
    }

    private void ConfigurarTriggerHover(GameObject targetObj, string nome, string descricao, GameObject abaTooltip, TextMeshProUGUI textoNome, TextMeshProUGUI textoConteudo)
    {
        if (targetObj == null || abaTooltip == null || textoConteudo == null) return;

        var hover = targetObj.GetComponent<UIHoverHandler>();
        if (hover == null) hover = targetObj.AddComponent<UIHoverHandler>();

        hover.onPointerEnterAction = () => {
            if (textoNome != null)
            {
                textoNome.text = nome;
                textoConteudo.text = descricao;
            }
            else
            {
                textoConteudo.text = $"<b>{nome}</b>\n\n{descricao}";
            }
            abaTooltip.SetActive(true);
        };

        hover.onPointerExitAction = () => {
            abaTooltip.SetActive(false);
        };
    }

    // ────────────────────────────────────────────────────
    // ESTÁGIO 3: CONFIRMAÇÃO FINAL DA EQUIPE
    // ────────────────────────────────────────────────────
    private void TransicionarParaEstagioConfirmacao()
    {
        OcultarEstagioTorres();
        ExibirEstagioConfirmacao();

        if (_comandanteSelecionado != null)
        {
            AtualizarNomeEIconeGlobal(_comandanteSelecionado);
            AtualizarPreview3D(_comandanteSelecionado.commanderPrefab, spawnPontoPreview, capsulePlaceholder);
        }

        MontarPreviewEquipe3DFinal();
        AtualizarAbaResumoEquipeUI();
        AtualizarEstadoProntoEInicio();
    }

    private void ExibirEstagioConfirmacao()
    {
        if (estagioConfirmacao.cmConfirmacao != null) estagioConfirmacao.cmConfirmacao.gameObject.SetActive(true);
        if (estagioConfirmacao.canvasConfirmacao != null) estagioConfirmacao.canvasConfirmacao.SetActive(true);
    }

    private void OcultarEstagioConfirmacao()
    {
        if (estagioConfirmacao.cmConfirmacao != null) estagioConfirmacao.cmConfirmacao.gameObject.SetActive(false);
        if (estagioConfirmacao.canvasConfirmacao != null) estagioConfirmacao.canvasConfirmacao.SetActive(false);
    }

    private void ConfigurarBotoesEstagio3()
    {
        if (estagioConfirmacao.botaoIniciarPartida != null)
            estagioConfirmacao.botaoIniciarPartida.onClick.AddListener(ConfirmarFinalEIniciarPartida);
    }

    private void AtualizarAbaResumoEquipeUI()
    {
        AtualizarContainerSlotsTorres(estagioConfirmacao.abaResumoEquipe1_4, 0, 4, false);
        AtualizarContainerSlotsTorres(estagioConfirmacao.abaResumoEquipe5_7, 4, 3, false);
    }

    private void AtualizarContainerSlotsTorres(Transform container, int startIndex, int maxCountInContainer, bool permitirRemover)
    {
        if (container == null) return;
        int childCount = container.childCount;

        if (childCount > 0)
        {
            for (int i = 0; i < childCount; i++)
            {
                int globalIndex = startIndex + i;
                Transform filho = container.GetChild(i);

                if (globalIndex < _torresEquipadas.Count && i < maxCountInContainer)
                {
                    CharacterBase torre = _torresEquipadas[globalIndex];
                    filho.gameObject.SetActive(true);
                    DefinirImagemNoCard(filho.gameObject, torre.characterIcon);

                    if (permitirRemover)
                    {
                        Button btn = filho.GetComponent<Button>();
                        if (btn == null) btn = filho.GetComponentInChildren<Button>(true);
                        if (btn != null)
                        {
                            btn.onClick.RemoveAllListeners();
                            int indexParaRemover = globalIndex;
                            btn.onClick.AddListener(() => {
                                if (indexParaRemover < _torresEquipadas.Count)
                                {
                                    _torresEquipadas.RemoveAt(indexParaRemover);
                                    SalvarSelecaoLocalNoGameData();
                                    AtualizarAbaTorresEquipadas();
                                    AtualizarEstadoProntoEInicio();
                                }
                            });
                        }
                    }
                }
                else
                {
                    LimparImagemDoCard(filho.gameObject);

                    Button btn = filho.GetComponent<Button>();
                    if (btn == null) btn = filho.GetComponentInChildren<Button>(true);
                    if (btn != null) btn.onClick.RemoveAllListeners();
                }
            }
            return;
        }

        if (slotTorreEquipadaPrefab != null || cardPrefab != null)
        {
            LimparContainer(container);
            for (int i = 0; i < maxCountInContainer; i++)
            {
                int globalIndex = startIndex + i;
                if (globalIndex >= _torresEquipadas.Count) break;

                CharacterBase torre = _torresEquipadas[globalIndex];
                int indexParaRemover = globalIndex;

                GameObject slotObj = (slotTorreEquipadaPrefab != null) 
                    ? Instantiate(slotTorreEquipadaPrefab, container)
                    : Instantiate(cardPrefab, container);

                if (slotObj != null)
                {
                    DefinirImagemNoCard(slotObj, torre.characterIcon);

                    if (permitirRemover)
                    {
                        Button btn = slotObj.GetComponent<Button>();
                        if (btn == null) btn = slotObj.GetComponentInChildren<Button>(true);
                        if (btn != null)
                        {
                            btn.onClick.RemoveAllListeners();
                            btn.onClick.AddListener(() => {
                                if (indexParaRemover < _torresEquipadas.Count)
                                {
                                    _torresEquipadas.RemoveAt(indexParaRemover);
                                    SalvarSelecaoLocalNoGameData();
                                    AtualizarAbaTorresEquipadas();
                                    AtualizarEstadoProntoEInicio();
                                }
                            });
                        }
                    }
                }
            }
        }
    }

    private void LimparImagemDoCard(GameObject card)
    {
        if (card == null) return;
        Image[] imagens = card.GetComponentsInChildren<Image>(true);
        if (imagens == null || imagens.Length == 0) return;

        foreach (var img in imagens)
        {
            string objName = img.gameObject.name.ToLower();
            if (objName.Contains("imagem") || objName.Contains("icon") || objName.Contains("personagem") || objName.Contains("portrait"))
            {
                img.sprite = null;
                img.enabled = false;
                return;
            }
        }
    }

    private void MontarPreviewEquipe3DFinal()
    {
        foreach (var m in _modelosFinalInstanciados)
            if (m != null) Destroy(m);
        _modelosFinalInstanciados.Clear();

        if (_comandanteSelecionado != null && _comandanteSelecionado.commanderPrefab != null && estagioConfirmacao.spawnComandanteFinal != null)
        {
            var modeloComandante = Instantiate(_comandanteSelecionado.commanderPrefab, estagioConfirmacao.spawnComandanteFinal.position, estagioConfirmacao.spawnComandanteFinal.rotation);
            DesativarScriptsDeGameplay(modeloComandante);
            _modelosFinalInstanciados.Add(modeloComandante);
        }

        if (estagioConfirmacao.spawnTorresFinal != null)
        {
            for (int i = 0; i < _torresEquipadas.Count && i < estagioConfirmacao.spawnTorresFinal.Length; i++)
            {
                var torre = _torresEquipadas[i];
                if (torre == null || torre.towerPrefab == null || estagioConfirmacao.spawnTorresFinal[i] == null) continue;

                var modeloTorre = Instantiate(torre.towerPrefab, estagioConfirmacao.spawnTorresFinal[i].position, estagioConfirmacao.spawnTorresFinal[i].rotation);
                DesativarScriptsDeGameplay(modeloTorre);
                _modelosFinalInstanciados.Add(modeloTorre);
            }
        }
    }

    private void ConfirmarFinalEIniciarPartida()
    {
        if (_comandanteSelecionado == null && personagensDisponiveis != null && personagensDisponiveis.Count > 0)
        {
            _comandanteSelecionado = personagensDisponiveis[0];
        }

        SalvarSelecaoLocalNoGameData();

        RegistrarEscolhaComandanteRede(_comandanteSelecionado);

        if (BuildManager.Instance != null && GameDataManager.Instance != null)
        {
            BuildManager.Instance.SetAvailableTowers(GameDataManager.Instance.equipeSelecionada);
        }

        var nm = NetworkManager.Singleton;
        if (GameModeManager.CurrentMode == GameMode.Multiplayer)
        {
            AtualizarEstadoProntoEInicio();

            if (nm == null || !nm.IsServer)
            {
                Debug.Log("[SelecaoEquipeFlowManager] Cliente aguardando o host iniciar a partida.");
                return;
            }

            if (!PodeHostIniciarPartida())
            {
                Debug.LogWarning("[SelecaoEquipeFlowManager] Partida bloqueada: selecao local, ready do lobby ou cache autoritativo incompleto.");
                return;
            }

            if (ExoBeasts.Managers.Loading.LoadingScreenUI.Instance != null)
                ExoBeasts.Managers.Loading.LoadingScreenUI.Instance.Show();

            nm.SceneManager.LoadScene(estagioConfirmacao.nomeDaCenaDoJogo, LoadSceneMode.Single);
        }
        else if (nm != null && nm.IsServer)
        {
            if (ExoBeasts.Managers.Loading.LoadingScreenUI.Instance != null)
                ExoBeasts.Managers.Loading.LoadingScreenUI.Instance.Show();

            nm.SceneManager.LoadScene(estagioConfirmacao.nomeDaCenaDoJogo, LoadSceneMode.Single);
        }
        else
        {
            StartCoroutine(IniciarPartidaSingleplayer());
        }
    }

    private IEnumerator IniciarPartidaSingleplayer()
    {
        var nm = NetworkManager.Singleton;
        if (nm != null)
        {
            if (nm.IsListening)
            {
                nm.Shutdown();
                float elapsed = 0f;
                while (nm.IsListening && elapsed < 3f)
                {
                    elapsed += Time.deltaTime;
                    yield return null;
                }
            }

            bool started = nm.StartHost();
            if (!started)
            {
                Debug.LogError("[SelecaoEquipeFlowManager] StartHost falhou. Carregando cena diretamente.");
                SceneManager.LoadScene(estagioConfirmacao.nomeDaCenaDoJogo);
                yield break;
            }

            if (_comandanteSelecionado != null)
                RegistrarEscolhaComandanteRede(_comandanteSelecionado);

            if (ExoBeasts.Managers.Loading.LoadingScreenUI.Instance != null)
                ExoBeasts.Managers.Loading.LoadingScreenUI.Instance.Show();

            nm.SceneManager.LoadScene(estagioConfirmacao.nomeDaCenaDoJogo, LoadSceneMode.Single);
        }
        else
        {
            SceneManager.LoadScene(estagioConfirmacao.nomeDaCenaDoJogo);
        }
    }

    // ────────────────────────────────────────────────────
    // UTILS
    // ────────────────────────────────────────────────────
    private void AtualizarNomeEIconeGlobal(CharacterBase p)
    {
        if (p == null) return;
        if (nomePersonagem != null) nomePersonagem.text = p.name.Replace("(Clone)", "");
        if (iconePersonagem != null && p.characterIcon != null) iconePersonagem.sprite = p.characterIcon;
    }

    private void DefinirImagemNoCard(GameObject card, Sprite icone)
    {
        if (card == null || icone == null) return;

        Image[] imagens = card.GetComponentsInChildren<Image>(true);
        if (imagens == null || imagens.Length == 0) return;

        foreach (var img in imagens)
        {
            string objName = img.gameObject.name.ToLower();
            if (objName.Contains("imagem") || objName.Contains("icon") || objName.Contains("personagem") || objName.Contains("portrait"))
            {
                img.sprite = icone;
                img.enabled = true;
                img.color = Color.white;
                return;
            }
        }

        foreach (var img in imagens)
        {
            string objName = img.gameObject.name.ToLower();
            if (!objName.Contains("borda") && !objName.Contains("overlay") && !objName.Contains("background") && !objName.Contains("fundo"))
            {
                img.sprite = icone;
                img.enabled = true;
                img.color = Color.white;
                return;
            }
        }

        imagens[0].sprite = icone;
        imagens[0].enabled = true;
        imagens[0].color = Color.white;
    }

    private void RegistrarEscolhaComandanteRede(CharacterBase c)
    {
        if (c == null) return;
        string cleanName = c.name.Replace("(Clone)", "");

        if (GameDataManager.Instance != null)
        {
            if (GameDataManager.Instance.bibliotecaOriginalPersonagens == null)
                GameDataManager.Instance.bibliotecaOriginalPersonagens = new List<CharacterBase>();

            var bibData = GameDataManager.Instance.bibliotecaOriginalPersonagens;
            int indexBib = bibData.FindIndex(item => item != null && item.name.Replace("(Clone)", "") == cleanName);
            if (indexBib < 0)
            {
                bibData.Add(c);
                indexBib = bibData.Count - 1;
            }

            var nm = NetworkManager.Singleton;
            if (LobbyManager.Instance != null)
                LobbyManager.Instance.SelectCharacter(indexBib);

            if (nm == null || nm.IsServer)
            {
                CharacterChoiceCache.SetHostCharacterIndex(indexBib, "SelecaoEquipeFlowManager");
            }
            else if (nm.IsClient)
            {
                if (nm.CustomMessagingManager != null)
                {
                    var writer = new FastBufferWriter(sizeof(int), Unity.Collections.Allocator.Temp);
                    writer.WriteValueSafe(indexBib);
                    nm.CustomMessagingManager.SendNamedMessage(k_CharChoiceMsg, NetworkManager.ServerClientId, writer);
                    writer.Dispose();
                }
            }

            Debug.Log($"[SelecaoEquipeFlowManager] Registrou escolha de comandante autoritativa: index={indexBib} ({cleanName})");
        }
    }

    private void InicializarSelecaoMultiplayer()
    {
        if (_selectionSceneInitialized) return;
        _selectionSceneInitialized = true;

        CalcularSlotsPermitidos();

        if (GameModeManager.CurrentMode != GameMode.Multiplayer)
            return;

        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnMemberUpdated += OnLobbyMemberChanged;
            LobbyManager.Instance.OnMemberJoined += OnLobbyMemberChanged;
        }

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsServer && nm.CustomMessagingManager != null)
        {
            nm.CustomMessagingManager.RegisterNamedMessageHandler(k_CharChoiceMsg, OnCharacterChoiceReceived);
            _multiplayerCallbacksRegistered = true;
        }

        SetSelectionReady(false, true);
    }

    private void ConfigurarPainelMultiplayer()
    {
        EnsurePainelMultiplayerBound();

        bool isMultiplayer = GameModeManager.CurrentMode == GameMode.Multiplayer;
        if (multiplayerUi.painelOutrosJogadores != null)
            multiplayerUi.painelOutrosJogadores.SetActive(isMultiplayer);

        if (!isMultiplayer)
            return;

        EnsureRuntimeMultiplayerPanel();

        if (multiplayerUi.botaoPronto != null)
        {
            multiplayerUi.botaoPronto.onClick.RemoveAllListeners();
            multiplayerUi.botaoPronto.onClick.AddListener(() => SetSelectionReady(!_isReady, true));
        }

        AtualizarListaJogadoresMultiplayer();
    }

    private void EnsurePainelMultiplayerBound()
    {
        if (multiplayerUi == null)
            multiplayerUi = new MultiplayerSelectionData();

        if (multiplayerUi.painelOutrosJogadores == null)
        {
            Transform found = FindSceneTransformByName("AbaDeOutrosJogadores");
            if (found != null)
                multiplayerUi.painelOutrosJogadores = found.gameObject;
        }
    }

    private void EnsureRuntimeMultiplayerPanel()
    {
        if (multiplayerUi.painelOutrosJogadores == null) return;

        RectTransform panelRect = multiplayerUi.painelOutrosJogadores.GetComponent<RectTransform>();
        if (panelRect != null)
        {
            panelRect.anchorMin = new Vector2(0.02f, 0.55f);
            panelRect.anchorMax = new Vector2(0.28f, 0.95f);
            panelRect.offsetMin = Vector2.zero;
            panelRect.offsetMax = Vector2.zero;
        }

        Image panelImage = multiplayerUi.painelOutrosJogadores.GetComponent<Image>();
        if (panelImage != null)
            panelImage.color = new Color(0.04f, 0.06f, 0.08f, 0.76f);

        VerticalLayoutGroup layout = multiplayerUi.painelOutrosJogadores.GetComponent<VerticalLayoutGroup>();
        if (layout == null)
            layout = multiplayerUi.painelOutrosJogadores.AddComponent<VerticalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 12, 12);
        layout.spacing = 8f;
        layout.childControlHeight = true;
        layout.childControlWidth = true;
        layout.childForceExpandHeight = false;
        layout.childForceExpandWidth = true;

        if (multiplayerUi.textoStatus == null)
            multiplayerUi.textoStatus = GetOrCreateText(multiplayerUi.painelOutrosJogadores.transform, "StatusMultiplayer", 18, FontStyles.Bold);

        if (multiplayerUi.containerListaJogadores == null)
        {
            GameObject container = GetOrCreateChild(multiplayerUi.painelOutrosJogadores.transform, "ListaJogadoresMultiplayer");
            if (container.GetComponent<VerticalLayoutGroup>() == null)
            {
                VerticalLayoutGroup containerLayout = container.AddComponent<VerticalLayoutGroup>();
                containerLayout.spacing = 4f;
                containerLayout.childControlHeight = true;
                containerLayout.childControlWidth = true;
                containerLayout.childForceExpandHeight = false;
                containerLayout.childForceExpandWidth = true;
            }
            multiplayerUi.containerListaJogadores = container.transform;
        }

        if (multiplayerUi.botaoPronto == null)
        {
            GameObject buttonObj = GetOrCreateChild(multiplayerUi.painelOutrosJogadores.transform, "BotaoProntoSelecao");
            Image image = buttonObj.GetComponent<Image>();
            if (image == null) image = buttonObj.AddComponent<Image>();
            image.color = new Color(0.8f, 0.8f, 0.8f, 1f);
            multiplayerUi.botaoPronto = buttonObj.GetComponent<Button>();
            if (multiplayerUi.botaoPronto == null) multiplayerUi.botaoPronto = buttonObj.AddComponent<Button>();

            RectTransform buttonRect = buttonObj.GetComponent<RectTransform>();
            if (buttonRect != null)
                buttonRect.sizeDelta = new Vector2(0f, 42f);
        }

        if (multiplayerUi.textoBotaoPronto == null)
            multiplayerUi.textoBotaoPronto = GetOrCreateText(multiplayerUi.botaoPronto.transform, "TextoPronto", 17, FontStyles.Bold);
    }

    private TextMeshProUGUI GetOrCreateText(Transform parent, string name, int fontSize, FontStyles style)
    {
        Transform existing = parent.Find(name);
        GameObject obj = existing != null ? existing.gameObject : new GameObject(name, typeof(RectTransform));
        if (existing == null)
            obj.transform.SetParent(parent, false);

        TextMeshProUGUI text = obj.GetComponent<TextMeshProUGUI>();
        if (text == null)
            text = obj.AddComponent<TextMeshProUGUI>();

        text.fontSize = fontSize;
        text.fontStyle = style;
        text.color = Color.white;
        text.raycastTarget = false;
        text.alignment = TextAlignmentOptions.MidlineLeft;
        return text;
    }

    private GameObject GetOrCreateChild(Transform parent, string name)
    {
        Transform existing = parent.Find(name);
        if (existing != null)
            return existing.gameObject;

        GameObject obj = new GameObject(name, typeof(RectTransform));
        obj.transform.SetParent(parent, false);
        return obj;
    }

    private Transform FindSceneTransformByName(string objectName)
    {
        Transform[] allTransforms = Resources.FindObjectsOfTypeAll<Transform>();
        foreach (Transform candidate in allTransforms)
        {
            if (candidate == null || candidate.gameObject.scene != gameObject.scene)
                continue;
            if (candidate.gameObject.name == objectName)
                return candidate;
        }
        return null;
    }

    private void CalcularSlotsPermitidos()
    {
        _slotsPermitidos.Clear();

        if (GameModeManager.CurrentMode != GameMode.Multiplayer || LobbyManager.Instance == null)
        {
            _slotsPermitidos.AddRange(new[] { 0, 1, 2, 3, 4, 5, 6, 7 });
            return;
        }

        List<LobbyMember> membros = LobbyManager.Instance.GetOrderedMembers();
        string meuId = ExoBeasts.Multiplayer.Auth.SessionManager.Instance?.GetUserId() ?? "";
        int meuIndice = LobbyManager.Instance.GetCanonicalMemberIndex(meuId);
        List<int> slots = PartySlotLayout.GetSlots(membros != null ? membros.Count : 1, meuIndice);
        _slotsPermitidos.AddRange(slots);

        if (GameDataManager.Instance != null && membros != null)
            GameDataManager.Instance.totalDeJogadores = membros.Count;
    }

    private int GetCommanderSlot()
    {
        return _slotsPermitidos.Count > 0 ? _slotsPermitidos[0] : 0;
    }

    private int GetMaxTorresPermitidas()
    {
        if (GameModeManager.CurrentMode == GameMode.Multiplayer)
            return Mathf.Max(0, _slotsPermitidos.Count - 1);

        return _maxTorres;
    }

    private bool HasLocalCommanderAndFirstTowerSelected()
    {
        return _comandanteSelecionado != null && _torresEquipadas.Count > 0;
    }

    private void SalvarSelecaoLocalNoGameData()
    {
        if (GameDataManager.Instance == null)
            return;

        if (GameDataManager.Instance.equipeSelecionada == null || GameDataManager.Instance.equipeSelecionada.Length < 8)
            GameDataManager.Instance.equipeSelecionada = new CharacterBase[8];

        CharacterBase[] equipe = GameDataManager.Instance.equipeSelecionada;

        if (GameModeManager.CurrentMode == GameMode.Multiplayer)
        {
            foreach (int slot in _slotsPermitidos)
            {
                if (slot >= 0 && slot < equipe.Length)
                    equipe[slot] = null;
            }

            int commanderSlot = GetCommanderSlot();
            if (commanderSlot >= 0 && commanderSlot < equipe.Length)
                equipe[commanderSlot] = _comandanteSelecionado;

            for (int i = 0; i < _torresEquipadas.Count && (i + 1) < _slotsPermitidos.Count; i++)
            {
                int slot = _slotsPermitidos[i + 1];
                if (slot >= 0 && slot < equipe.Length)
                    equipe[slot] = _torresEquipadas[i];
            }
        }
        else
        {
            equipe[0] = _comandanteSelecionado;
            for (int i = 1; i < equipe.Length; i++)
                equipe[i] = null;
            for (int i = 0; i < _torresEquipadas.Count && (i + 1) < equipe.Length; i++)
                equipe[i + 1] = _torresEquipadas[i];
        }

        GameDataManager.Instance.SaveGame();
    }

    private void AtualizarEstadoProntoEInicio()
    {
        bool localComplete = HasLocalCommanderAndFirstTowerSelected();

        if (!localComplete && _isReady)
            SetSelectionReady(false, true);

        bool isMultiplayer = GameModeManager.CurrentMode == GameMode.Multiplayer;
        bool isHost = IsLocalLobbyHost();

        if (multiplayerUi.painelOutrosJogadores != null)
            multiplayerUi.painelOutrosJogadores.SetActive(isMultiplayer);

        if (multiplayerUi.botaoPronto != null)
            multiplayerUi.botaoPronto.interactable = isMultiplayer && localComplete;

        if (multiplayerUi.textoBotaoPronto != null)
            multiplayerUi.textoBotaoPronto.text = _isReady ? "Pronto" : localComplete ? "Ficar pronto" : "Escolha comandante + torre";

        if (multiplayerUi.botaoPronto != null)
        {
            Image image = multiplayerUi.botaoPronto.GetComponent<Image>();
            if (image != null)
                image.color = _isReady ? new Color(0.2f, 0.65f, 0.25f, 1f) : new Color(0.8f, 0.8f, 0.8f, 1f);
        }

        if (multiplayerUi.textoStatus != null)
        {
            multiplayerUi.textoStatus.text = isMultiplayer
                ? localComplete ? "Selecao local completa" : "Escolha comandante e primeira torre"
                : "";
        }

        if (estagioConfirmacao.botaoIniciarPartida != null)
        {
            if (isMultiplayer)
            {
                estagioConfirmacao.botaoIniciarPartida.gameObject.SetActive(isHost);
                estagioConfirmacao.botaoIniciarPartida.interactable = isHost && PodeHostIniciarPartida();
            }
            else
            {
                estagioConfirmacao.botaoIniciarPartida.gameObject.SetActive(true);
                estagioConfirmacao.botaoIniciarPartida.interactable = localComplete;
            }
        }

        AtualizarListaJogadoresMultiplayer();
    }

    private void AtualizarListaJogadoresMultiplayer()
    {
        if (GameModeManager.CurrentMode != GameMode.Multiplayer || multiplayerUi.containerListaJogadores == null)
            return;

        foreach (Transform child in multiplayerUi.containerListaJogadores)
            Destroy(child.gameObject);

        List<LobbyMember> members = LobbyManager.Instance?.GetOrderedMembers() ?? new List<LobbyMember>();
        string localUid = ExoBeasts.Multiplayer.Auth.SessionManager.Instance?.GetUserId() ?? "";
        LobbyInfo lobby = LobbyManager.Instance?.GetCurrentLobby();

        for (int i = 0; i < members.Count; i++)
        {
            LobbyMember member = members[i];
            TextMeshProUGUI row = GetOrCreateText(multiplayerUi.containerListaJogadores, $"Jogador_{i + 1}", 15, FontStyles.Normal);
            bool isMe = member.productUserId == localUid;
            bool isHost = lobby != null && lobby.hostProductUserId == member.productUserId;
            string ready = member.isReady ? "OK" : "...";
            string host = isHost ? " Host" : "";
            string me = isMe ? " Voce" : "";
            row.text = $"{i + 1}. {member.displayName}{host}{me} [{ready}]";
            row.color = isMe ? Color.yellow : member.isReady ? Color.green : Color.white;
        }
    }

    private bool IsLocalLobbyHost()
    {
        LobbyInfo lobby = LobbyManager.Instance?.GetCurrentLobby();
        string localUid = ExoBeasts.Multiplayer.Auth.SessionManager.Instance?.GetUserId() ?? "";
        return lobby != null && !string.IsNullOrEmpty(localUid) && lobby.hostProductUserId == localUid;
    }

    private bool PodeHostIniciarPartida()
    {
        if (!HasLocalCommanderAndFirstTowerSelected())
            return false;

        if (GameModeManager.CurrentMode != GameMode.Multiplayer)
            return true;

        if (!IsLocalLobbyHost())
            return false;

        List<LobbyMember> members = LobbyManager.Instance?.GetOrderedMembers();
        if (members == null || members.Count == 0 || !members.TrueForAll(m => m.isReady))
            return false;

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

    private void SetSelectionReady(bool ready, bool notifyLobby)
    {
        if (ready && !HasLocalCommanderAndFirstTowerSelected())
            ready = false;

        _isReady = ready;

        if (notifyLobby && GameModeManager.CurrentMode == GameMode.Multiplayer && LobbyManager.Instance != null)
            LobbyManager.Instance.SetReady(ready);

        AtualizarEstadoProntoEInicio();
    }

    private void OnLobbyMemberChanged(LobbyMember _)
    {
        CalcularSlotsPermitidos();
        AtualizarEstadoProntoEInicio();
    }

    private void OnCharacterChoiceReceived(ulong senderId, FastBufferReader reader)
    {
        reader.ReadValueSafe(out int charIdx);
        CharacterChoiceCache.SetClientCharacterIndex(senderId, charIdx, "SelecaoEquipeFlowManager.CustomMessage");
        AtualizarEstadoProntoEInicio();
        Debug.Log($"[SelecaoEquipeFlowManager] Commander choice registered: clientId={senderId}, index={charIdx}");
    }

    private void SetCanvasPersonagemActive(bool active)
    {
        if (estagioComandante.canvasPersonagem == null)
            return;

        EnsurePainelMultiplayerBound();

        bool keepMultiplayerPanel = GameModeManager.CurrentMode == GameMode.Multiplayer &&
                                    multiplayerUi.painelOutrosJogadores != null &&
                                    multiplayerUi.painelOutrosJogadores.transform.IsChildOf(estagioComandante.canvasPersonagem.transform);

        if (!keepMultiplayerPanel)
        {
            estagioComandante.canvasPersonagem.SetActive(active);
            return;
        }

        estagioComandante.canvasPersonagem.SetActive(true);
        foreach (Transform child in estagioComandante.canvasPersonagem.transform)
        {
            bool isPanel = child.gameObject == multiplayerUi.painelOutrosJogadores;
            child.gameObject.SetActive(active || isPanel);
        }
    }

    private void AtualizarPreview3D(GameObject prefab, Transform spawnTransform, GameObject capsulePlaceholder)
    {
        if (_modeloPreviewAtual != null)
        {
            Destroy(_modeloPreviewAtual);
            _modeloPreviewAtual = null;
        }

        if (prefab == null || spawnTransform == null)
        {
            if (capsulePlaceholder != null) capsulePlaceholder.SetActive(true);
            return;
        }

        if (capsulePlaceholder != null) capsulePlaceholder.SetActive(false);

        _modeloPreviewAtual = Instantiate(prefab, spawnTransform.position, spawnTransform.rotation);

        if (olharParaAlvo != null)
        {
            _modeloPreviewAtual.transform.LookAt(olharParaAlvo.position);
        }

        DesativarScriptsDeGameplay(_modeloPreviewAtual);
    }

    private void DesativarScriptsDeGameplay(GameObject obj)
    {
        if (obj == null) return;

        var rb = obj.GetComponentInChildren<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        foreach (var mb in obj.GetComponentsInChildren<MonoBehaviour>())
        {
            if (mb == null) continue;
            mb.enabled = false;
        }

        foreach (var col in obj.GetComponentsInChildren<Collider>())
            col.enabled = false;
    }

    private void LimparContainer(Transform container)
    {
        if (container == null) return;
        foreach (Transform filho in container)
            Destroy(filho.gameObject);
    }

    private void OnDestroy()
    {
        if (LobbyManager.Instance != null)
        {
            LobbyManager.Instance.OnMemberUpdated -= OnLobbyMemberChanged;
            LobbyManager.Instance.OnMemberJoined -= OnLobbyMemberChanged;
        }

        var nm = NetworkManager.Singleton;
        if (_multiplayerCallbacksRegistered && nm != null && nm.IsServer && nm.CustomMessagingManager != null)
            nm.CustomMessagingManager.UnregisterNamedMessageHandler(k_CharChoiceMsg);

        if (_modeloPreviewAtual != null) Destroy(_modeloPreviewAtual);
        foreach (var m in _modelosFinalInstanciados)
            if (m != null) Destroy(m);
    }
}
