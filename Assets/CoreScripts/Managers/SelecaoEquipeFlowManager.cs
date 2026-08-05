using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.Cinemachine;
using ExoBeasts.Managers;
using ExoBeasts.Multiplayer.Core;
using UnityEngine.SceneManagement;

/// <summary>
/// ── SelecaoEquipeFlowManager ──────────────────────────────
/// Gerenciador completo do fluxo da CenaSeleção.
///
/// Inspector Simplificado em 3 Estágios:
///  1. Seleção de Comandante (CM_Personagem + Canvas_Personagem)
///     - Grid de escolha de personagens
///     - Aba TipoPersonagem (Botões Comandante / Torre)
///     - 1 Aba de Hover Única (Aba + Texto Único que muda com o mouse)
///
///  2. Seleção de Torres (CM_Torres + Canvas_Torres)
///     - Grid de escolha de torres (Comandante desabilitado)
///     - 3 Caminhos de Upgrade (com 5 níveis cada)
///     - 1 Aba de Hover Única (Aba + Texto Único que muda com o mouse)
///     - Lista das 7 torres equipadas
///
///  3. Confirmação Final (CM_Confirmacao + Canvas_Confirmacao)
///     - Visualização 3D do Comandante + Torres atrás
///     - Botão Iniciar Partida
/// ──────────────────────────────────────────────────────────
/// </summary>
public class SelecaoEquipeFlowManager : MonoBehaviour
{
    // ── CONFIGURAÇÃO DE PERSONAGENS ─────────────────────
    [Header(" Configuração da Lista de Personagens")]
    [Tooltip("Lista de personagens disponíveis. Se estiver vazia, usará automaticamente GameDataManager.Instance.personagensDoJogador")]
    public List<CharacterBase> personagensDisponiveis = new List<CharacterBase>();
    
    [Tooltip("Prefab do Card de Seleção do Personagem (deve possuir Image e Button no root)")]
    public GameObject cardPrefab;

    [Tooltip("Prefab do Slot de Torre Equipada no Canvas de Torres (deve possuir Image e Button no root)")]
    public GameObject slotTorreEquipadaPrefab;

    // ── ESTÁGIO 1: COMANDANTE ───────────────────────────
    [System.Serializable]
    public class EstagioComandanteData
    {
        [Header("Câmera e Canvas")]
        public CinemachineCamera cmPersonagem;
        public GameObject canvasPersonagem;

        [Header("Preview 3D no Pedestal")]
        public Transform spawnPontoPreview;
        public GameObject capsulePlaceholder;

        [Header("Layouts / Abas UI")]
        [Tooltip("Content/Layout onde os cards de personagens serão instanciados para escolha")]
        public Transform abaEscolhaPersonagem;

        [Tooltip("Painel 'TipoPersonagem' (contém os botões Comandante / Torre — ativa ao clicar num personagem)")]
        public GameObject abaTipoPersonagem;

        [Tooltip("Aba que mostra a visualização das habilidades do Comandante")]
        public GameObject abaDetalhesComandante;

        [Tooltip("Aba que mostra as especificações da Torre do personagem")]
        public GameObject abaDetalhesTorre;

        [Header("Botões do TipoPersonagem")]
        public Button botaoSubAbaComandante;
        public Button botaoSubAbaTorre;

        [Header("Info Geral do Personagem")]
        public TextMeshProUGUI nomePersonagem;
        public Image iconePersonagem;

        [Header("Aba Única de Hover (Tooltip) do Canvas Comandante")]
        [Tooltip("Painel/Aba única de Hover que abre ao passar o mouse por cima de habilidades ou caminhos")]
        public GameObject abaHoverTooltip;
        [Tooltip("Único componente de texto dentro da Aba de Hover onde Nome e Descrição são inseridos dinamicamente")]
        public TextMeshProUGUI textoHoverConteudo;

        [Header("Ícones / Containers de Habilidades no Canvas")]
        public GameObject iconPassiva;
        public GameObject iconHabilidade1;
        public GameObject iconHabilidade2;
        public GameObject iconUltimate;

        [Header("Containers dos 3 Caminhos no Kit da Torre")]
        public GameObject containerCaminho1;
        public GameObject containerCaminho2;
        public GameObject containerCaminho3;

        [Header("Botão Confirmar Comandante")]
        public Button botaoConfirmarComandante;

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
        [Tooltip("Content/Layout onde os cards de personagens para torres serão instanciados")]
        public Transform abaEscolhaPersonagem;

        [Tooltip("Aba de Caminhos da Torre (mostra os 3 caminhos e o botão para equipar)")]
        public GameObject abaCaminhoTorre;

        [Tooltip("Content/Layout com os slots das torres já equipadas (máx 7)")]
        public Transform abaTorresEquipadas;

        [Header("Aba Única de Hover (Tooltip) do Canvas Torres")]
        [Tooltip("Painel/Aba única de Hover que abre ao passar o mouse por cima dos caminhos de torre")]
        public GameObject abaHoverTooltip;
        [Tooltip("Único componente de texto dentro da Aba de Hover onde Nome e os 5 Níveis de Upgrade são inseridos")]
        public TextMeshProUGUI textoHoverConteudo;

        [Header("Containers dos 3 Caminhos de Upgrade")]
        public GameObject containerCaminho1;
        public GameObject containerCaminho2;
        public GameObject containerCaminho3;

        [Header("Ação Equipar Torre")]
        public Button botaoAdicionarTorre;
        public TextMeshProUGUI textoNomeTorreEmFoco;

        [Header("Botão Confirmar Equipe de Torres")]
        public Button botaoConfirmarTorres;
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

        [Header("Aba / Painel Final")]
        public GameObject abaResumoEquipe;
        public Button botaoIniciarPartida;

        [Header("Cena do Jogo")]
        public string nomeDaCenaDoJogo = "CenaMapaNOVO";
    }

    [Header(" ESTÁGIO 3: CONFIRMAÇÃO FINAL DA EQUIPE")]
    public EstagioConfirmacaoFinalData estagioConfirmacao;

    // ── ESTADO INTERNO DE SELEÇÃO ───────────────────────
    private CharacterBase _comandanteSelecionado;
    private CharacterBase _personagemVisualizacaoComandante;
    private CharacterBase _torreVisualizacaoTorres;
    private List<CharacterBase> _torresEquipadas = new List<CharacterBase>();
    private GameObject _modeloPreviewAtual;
    private List<GameObject> _modelosFinalInstanciados = new List<GameObject>();
    private int _maxTorres = 7;

    // ────────────────────────────────────────────────────
    void Start()
    {
        IniciarFluxo();
    }

    public void IniciarFluxo()
    {
        OcultarEstagioTorres();
        OcultarEstagioConfirmacao();

        ConfigurarBotoesEstagio1();
        ConfigurarBotoesEstagio2();
        ConfigurarBotoesEstagio3();

        StartCoroutine(CarregarPersonagensEPopularGrids());
    }

    private IEnumerator CarregarPersonagensEPopularGrids()
    {
        yield return new WaitUntil(() => GameDataManager.Instance != null);

        if (personagensDisponiveis == null || personagensDisponiveis.Count == 0)
        {
            personagensDisponiveis = new List<CharacterBase>(GameDataManager.Instance.personagensDoJogador);
        }

        ExibirEstagioComandante();
        PopularGridComandante();
    }

    // ────────────────────────────────────────────────────
    // ESTÁGIO 1: SELEÇÃO DE COMANDANTE
    // ────────────────────────────────────────────────────
    private void ExibirEstagioComandante()
    {
        if (estagioComandante.cmPersonagem != null) estagioComandante.cmPersonagem.gameObject.SetActive(true);
        if (estagioComandante.canvasPersonagem != null) estagioComandante.canvasPersonagem.SetActive(true);

        if (estagioComandante.capsulePlaceholder != null) estagioComandante.capsulePlaceholder.SetActive(true);
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

        if (estagioComandante.botaoPopupConfirmar != null)
            estagioComandante.botaoPopupConfirmar.onClick.AddListener(ConfirmarComandante);

        if (estagioComandante.botaoPopupCancelar != null)
            estagioComandante.botaoPopupCancelar.onClick.AddListener(() => {
                if (estagioComandante.popupConfirmacao != null) estagioComandante.popupConfirmacao.SetActive(false);
            });
    }

    private void PopularGridComandante()
    {
        if (estagioComandante.abaEscolhaPersonagem == null || cardPrefab == null) return;

        LimparContainer(estagioComandante.abaEscolhaPersonagem);

        foreach (var p in personagensDisponiveis)
        {
            if (p == null) continue;
            CharacterBase capturedP = p;

            GameObject card = Instantiate(cardPrefab, estagioComandante.abaEscolhaPersonagem);
            
            Image img = card.GetComponent<Image>();
            if (img != null && capturedP.characterIcon != null)
                img.sprite = capturedP.characterIcon;

            Button btn = card.GetComponent<Button>();
            if (btn != null)
                btn.onClick.AddListener(() => AoSelecionarPersonagemComandante(capturedP));
        }
    }

    private void AoSelecionarPersonagemComandante(CharacterBase p)
    {
        _personagemVisualizacaoComandante = p;

        if (estagioComandante.nomePersonagem != null)
            estagioComandante.nomePersonagem.text = p.name.Replace("(Clone)", "");
        if (estagioComandante.iconePersonagem != null && p.characterIcon != null)
            estagioComandante.iconePersonagem.sprite = p.characterIcon;

        if (estagioComandante.abaTipoPersonagem != null)
            estagioComandante.abaTipoPersonagem.SetActive(true);

        ConfigurarHoverHabilidades(p);
        ConfigurarHoverCaminhosTorre(p, estagioComandante.containerCaminho1, estagioComandante.containerCaminho2, estagioComandante.containerCaminho3, estagioComandante.abaHoverTooltip, estagioComandante.textoHoverConteudo);

        AbrirSubAbaComandante();

        AtualizarPreview3D(p.commanderPrefab, estagioComandante.spawnPontoPreview, estagioComandante.capsulePlaceholder);
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

    // ────────────────────────────────────────────────────
    // CONFIGURAÇÃO DO HOVER ÚNICO (Habilidades & Caminhos)
    // ────────────────────────────────────────────────────
    private void ConfigurarHoverHabilidades(CharacterBase p)
    {
        ConfigurarTriggerHover(estagioComandante.iconPassiva, p.passive?.abilityName ?? "Passiva", p.passive?.description ?? "", estagioComandante.abaHoverTooltip, estagioComandante.textoHoverConteudo);
        ConfigurarTriggerHover(estagioComandante.iconHabilidade1, p.ability1?.abilityName ?? "Habilidade 1", p.ability1?.description ?? "", estagioComandante.abaHoverTooltip, estagioComandante.textoHoverConteudo);
        ConfigurarTriggerHover(estagioComandante.iconHabilidade2, p.ability2?.abilityName ?? "Habilidade 2", p.ability2?.description ?? "", estagioComandante.abaHoverTooltip, estagioComandante.textoHoverConteudo);
        ConfigurarTriggerHover(estagioComandante.iconUltimate, p.ultimate?.abilityName ?? "Ultimate", p.ultimate?.description ?? "", estagioComandante.abaHoverTooltip, estagioComandante.textoHoverConteudo);
    }

    private void ConfigurarHoverCaminhosTorre(CharacterBase p, GameObject c1, GameObject c2, GameObject c3, GameObject abaTooltip, TextMeshProUGUI textoTooltip)
    {
        ConfigurarTriggerHoverCaminho(c1, 0, p.upgradePaths, abaTooltip, textoTooltip);
        ConfigurarTriggerHoverCaminho(c2, 1, p.upgradePaths, abaTooltip, textoTooltip);
        ConfigurarTriggerHoverCaminho(c3, 2, p.upgradePaths, abaTooltip, textoTooltip);
    }

    private void ConfigurarTriggerHover(GameObject targetObj, string nome, string descricao, GameObject abaTooltip, TextMeshProUGUI textoTooltip)
    {
        if (targetObj == null || abaTooltip == null || textoTooltip == null) return;

        var hover = targetObj.GetComponent<UIHoverHandler>();
        if (hover == null) hover = targetObj.AddComponent<UIHoverHandler>();

        hover.onPointerEnterAction = () => {
            textoTooltip.text = $"<b>{nome}</b>\n\n{descricao}";
            abaTooltip.SetActive(true);
        };

        hover.onPointerExitAction = () => {
            abaTooltip.SetActive(false);
        };
    }

    private void ConfigurarTriggerHoverCaminho(GameObject targetObj, int pathIndex, List<UpgradePath> paths, GameObject abaTooltip, TextMeshProUGUI textoTooltip)
    {
        if (targetObj == null || abaTooltip == null || textoTooltip == null) return;

        bool temPath = (paths != null && pathIndex < paths.Count && paths[pathIndex] != null);
        targetObj.SetActive(temPath);

        if (!temPath) return;

        var path = paths[pathIndex];
        var hover = targetObj.GetComponent<UIHoverHandler>();
        if (hover == null) hover = targetObj.AddComponent<UIHoverHandler>();

        hover.onPointerEnterAction = () => {
            string conteudo = $"<b>Caminho {pathIndex + 1}: {path.pathName}</b>\n\n";

            if (path.upgradesInPath != null && path.upgradesInPath.Count > 0)
            {
                conteudo += "<b>Níveis de Upgrade (1 a 5):</b>\n";
                for (int i = 0; i < path.upgradesInPath.Count; i++)
                {
                    var up = path.upgradesInPath[i];
                    if (up != null)
                        conteudo += $"• <b>Nível {i + 1} ({up.upgradeName}):</b> {up.description}\n";
                }
            }
            else
            {
                conteudo += "Caminho de upgrade da torre.";
            }

            textoTooltip.text = conteudo;
            abaTooltip.SetActive(true);
        };

        hover.onPointerExitAction = () => {
            abaTooltip.SetActive(false);
        };
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

        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.equipeSelecionada[0] = _comandanteSelecionado;
            GameDataManager.Instance.SaveGame();
        }

        RegistrarEscolhaComandanteRede(_comandanteSelecionado);

        TransicionarParaEstagioTorres();
    }

    private void OcultarEstagioComandante()
    {
        if (estagioComandante.cmPersonagem != null) estagioComandante.cmPersonagem.gameObject.SetActive(false);
        if (estagioComandante.canvasPersonagem != null) estagioComandante.canvasPersonagem.SetActive(false);
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

        if (estagioTorres.botaoConfirmarTorres != null)
            estagioTorres.botaoConfirmarTorres.onClick.AddListener(TransicionarParaEstagioConfirmacao);
    }

    private void PopularGridTorres()
    {
        if (estagioTorres.abaEscolhaPersonagem == null || cardPrefab == null) return;

        LimparContainer(estagioTorres.abaEscolhaPersonagem);

        foreach (var p in personagensDisponiveis)
        {
            if (p == null) continue;
            CharacterBase capturedP = p;

            GameObject card = Instantiate(cardPrefab, estagioTorres.abaEscolhaPersonagem);
            
            Image img = card.GetComponent<Image>();
            if (img != null && capturedP.characterIcon != null)
                img.sprite = capturedP.characterIcon;

            Button btn = card.GetComponent<Button>();

            bool isComandante = (_comandanteSelecionado != null && capturedP.name == _comandanteSelecionado.name);
            if (isComandante)
            {
                if (img != null) img.color = new Color(0.3f, 0.3f, 0.3f, 0.7f);
                if (btn != null) btn.interactable = false;
            }
            else
            {
                if (btn != null)
                    btn.onClick.AddListener(() => AoSelecionarPersonagemTorre(capturedP));
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

        ConfigurarHoverCaminhosTorre(p, estagioTorres.containerCaminho1, estagioTorres.containerCaminho2, estagioTorres.containerCaminho3, estagioTorres.abaHoverTooltip, estagioTorres.textoHoverConteudo);
    }

    private void EquiparTorreEmFoco()
    {
        if (_torreVisualizacaoTorres == null) return;

        if (_torresEquipadas.Count < _maxTorres)
        {
            _torresEquipadas.Add(_torreVisualizacaoTorres);
            AtualizarAbaTorresEquipadas();
        }
        else
        {
            Debug.LogWarning("[SelecaoEquipeFlowManager] Limite máximo de 7 torres atingido.");
        }
    }

    private void AtualizarAbaTorresEquipadas()
    {
        if (estagioTorres.abaTorresEquipadas == null) return;

        LimparContainer(estagioTorres.abaTorresEquipadas);

        for (int i = 0; i < _torresEquipadas.Count; i++)
        {
            int index = i;
            CharacterBase torre = _torresEquipadas[i];

            GameObject slotObj = (slotTorreEquipadaPrefab != null) 
                ? Instantiate(slotTorreEquipadaPrefab, estagioTorres.abaTorresEquipadas)
                : Instantiate(cardPrefab, estagioTorres.abaTorresEquipadas);

            Image img = slotObj.GetComponent<Image>();
            if (img != null && torre.characterIcon != null)
                img.sprite = torre.characterIcon;

            Button btn = slotObj.GetComponent<Button>();
            if (btn != null)
            {
                btn.onClick.AddListener(() => {
                    _torresEquipadas.RemoveAt(index);
                    AtualizarAbaTorresEquipadas();
                });
            }
        }
    }

    // ────────────────────────────────────────────────────
    // ESTÁGIO 3: CONFIRMAÇÃO FINAL DA EQUIPE
    // ────────────────────────────────────────────────────
    private void TransicionarParaEstagioConfirmacao()
    {
        OcultarEstagioTorres();
        ExibirEstagioConfirmacao();
        MontarPreviewEquipe3DFinal();
    }

    private void ExibirEstagioConfirmacao()
    {
        if (estagioConfirmacao.cmConfirmacao != null) estagioConfirmacao.cmConfirmacao.gameObject.SetActive(true);
        if (estagioConfirmacao.canvasConfirmacao != null) estagioConfirmacao.canvasConfirmacao.SetActive(true);
        if (estagioConfirmacao.abaResumoEquipe != null) estagioConfirmacao.abaResumoEquipe.SetActive(true);
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
        if (GameDataManager.Instance != null)
        {
            GameDataManager.Instance.equipeSelecionada[0] = _comandanteSelecionado;
            for (int i = 0; i < _torresEquipadas.Count && (i + 1) < GameDataManager.Instance.equipeSelecionada.Length; i++)
                GameDataManager.Instance.equipeSelecionada[i + 1] = _torresEquipadas[i];
            
            GameDataManager.Instance.SaveGame();
        }

        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm != null && nm.IsServer)
        {
            if (ExoBeasts.Managers.Loading.LoadingScreenUI.Instance != null)
                ExoBeasts.Managers.Loading.LoadingScreenUI.Instance.Show();

            nm.SceneManager.LoadScene(estagioConfirmacao.nomeDaCenaDoJogo, LoadSceneMode.Single);
        }
        else if (nm != null)
        {
            StartCoroutine(IniciarPartidaSingleplayer());
        }
        else
        {
            SceneManager.LoadScene(estagioConfirmacao.nomeDaCenaDoJogo);
        }
    }

    private IEnumerator IniciarPartidaSingleplayer()
    {
        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm == null) yield break;

        if (nm.IsListening)
        {
            nm.Shutdown();
            float elapsed = 0f;
            while (nm.IsListening && elapsed < 3f)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            if (nm.IsListening) yield break;
        }

        nm.StartHost();

        if (ExoBeasts.Managers.Loading.LoadingScreenUI.Instance != null)
            ExoBeasts.Managers.Loading.LoadingScreenUI.Instance.Show();

        nm.SceneManager.LoadScene(estagioConfirmacao.nomeDaCenaDoJogo, LoadSceneMode.Single);
    }

    // ────────────────────────────────────────────────────
    // UTILS
    // ────────────────────────────────────────────────────
    private void RegistrarEscolhaComandanteRede(CharacterBase c)
    {
        var bib = GameDataManager.Instance?.bibliotecaOriginalPersonagens;
        if (bib == null || c == null) return;

        string cleanName = c.name.Replace("(Clone)", "");
        int idx = bib.FindIndex(item => item != null && item.name == cleanName);
        if (idx < 0) return;

        var nm = Unity.Netcode.NetworkManager.Singleton;
        if (nm == null) return;

        if (nm.IsServer)
            CharacterChoiceCache.SetHostCharacterIndex(idx, "SelecaoEquipeFlowManager");
        else if (nm.IsClient)
        {
            var writer = new Unity.Netcode.FastBufferWriter(sizeof(int), Unity.Collections.Allocator.Temp);
            writer.WriteValueSafe(idx);
            nm.CustomMessagingManager.SendNamedMessage("ExoBeasts.CharacterChoice", Unity.Netcode.NetworkManager.ServerClientId, writer);
            writer.Dispose();
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
        DesativarScriptsDeGameplay(_modeloPreviewAtual);
    }

    private void DesativarScriptsDeGameplay(GameObject obj)
    {
        if (obj == null) return;

        var rb = obj.GetComponentInChildren<Rigidbody>();
        if (rb != null) rb.isKinematic = true;

        foreach (var mb in obj.GetComponentsInChildren<MonoBehaviour>())
        {
            if (mb == null || mb is Animator) continue;
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
        if (_modeloPreviewAtual != null) Destroy(_modeloPreviewAtual);
        foreach (var m in _modelosFinalInstanciados)
            if (m != null) Destroy(m);
    }
}
