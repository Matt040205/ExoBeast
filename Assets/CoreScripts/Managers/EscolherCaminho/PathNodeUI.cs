using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

public enum EstadoNoCaminho
{
    Concluido,   // Início / Caminhos passados já superados
    Disponivel,  // Possíveis caminhos atuais (clicáveis)
    Futuro       // Caminhos futuros (bloqueados até avançar)
}

/// <summary>
/// Componente colocado em cada nó clicável da tela de seleção de caminho.
/// Suporta 3 estados visuais e funcionais:
///  - Concluido: caminhos passados (desabilitado com opacidade reduzida/check)
///  - Disponivel: caminhos da etapa atual (clicável com hover)
///  - Futuro: caminhos das próximas etapas (bloqueado com opacidade reduzida/cadeado)
/// </summary>

[RequireComponent(typeof(Button))]
public class PathNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Dados do Mapa")]
    [Tooltip("ScriptableObject com os dados do mapa representado por este nó")]
    public MapData mapData;

    [Header("Progressão do Caminho")]
    [Tooltip("Índice do nível/etapa deste nó:\n" +
             " 0 = Início / Primeira etapa\n" +
             " 1 = Segunda etapa\n" +
             " 2 = Terceira etapa, etc.")]
    public int nivelDoNo = 0;

    [Header("Referências Visuais (Opcionais)")]
    [Tooltip("Imagem principal do nó (para alteração de cor/opacidade)")]
    public Image iconeImagem;

    [Tooltip("Overlay exibido quando o nó está no estado Futuro (Bloqueado)")]
    public GameObject overlayBloqueado;

    [Tooltip("Overlay exibido quando o nó está no estado Concluido (Passado)")]
    public GameObject overlayConcluido;

    [Header("Animação de Hover (Apenas nos Disponíveis)")]
    [Tooltip("Escala ao passar o mouse sobre um nó disponível")]
    public float escalaHover = 1.15f;
    [Tooltip("Velocidade da animação de escala")]
    public float velocidadeAnimacao = 8f;

    // Estado Runtime
    public EstadoNoCaminho EstadoAtual { get; private set; } = EstadoNoCaminho.Futuro;

    // Referências internas
    private CaminhoManager _caminhoManager;
    private Vector3 _escalaOriginal;
    private Vector3 _escalaAlvo;
    private Button _botao;
    private CanvasGroup _canvasGroup;

    private void Awake()
    {
        _botao = GetComponent<Button>();
        _escalaOriginal = transform.localScale;
        _escalaAlvo = _escalaOriginal;

        // Tenta obter ou criar um CanvasGroup para controle automático de opacidade por estado
        _canvasGroup = GetComponent<CanvasGroup>();
        if (_canvasGroup == null)
            _canvasGroup = gameObject.AddComponent<CanvasGroup>();
    }

    private void Start()
    {
        _caminhoManager = FindObjectOfType<CaminhoManager>();

        if (_caminhoManager == null)
            Debug.LogWarning($"[PathNodeUI] CaminhoManager não encontrado na cena! Nó: {gameObject.name}");

        _botao.onClick.RemoveListener(OnNodeClicked);
        _botao.onClick.AddListener(OnNodeClicked);

        // Aplica o estado inicial
        AtualizarEstadoBloqueio();
    }

    private void Update()
    {
        // Animação de hover suave — apenas em nós no estado Disponivel
        if (EstadoAtual == EstadoNoCaminho.Disponivel)
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _escalaAlvo, Time.deltaTime * velocidadeAnimacao);
        }
        else
        {
            transform.localScale = _escalaOriginal;
        }
    }

    /// <summary>
    /// Recalcula se o nó é Concluido, Disponivel ou Futuro com base no nivelAtual do CaminhoManager.
    /// </summary>
    public void AtualizarEstadoBloqueio()
    {
        if (_caminhoManager == null)
            _caminhoManager = FindObjectOfType<CaminhoManager>();

        int nivelAtual = _caminhoManager != null ? _caminhoManager.NivelAtualDoCaminho : 0;

        if (nivelDoNo < nivelAtual)
        {
            EstadoAtual = EstadoNoCaminho.Concluido;
        }
        else if (nivelDoNo == nivelAtual)
        {
            EstadoAtual = EstadoNoCaminho.Disponivel;
        }
        else
        {
            EstadoAtual = EstadoNoCaminho.Futuro;
        }

        AplicarEstiloVisual();
    }

    private void AplicarEstiloVisual()
    {
        switch (EstadoAtual)
        {
            case EstadoNoCaminho.Disponivel:
                _botao.interactable = true;
                if (_canvasGroup != null) _canvasGroup.alpha = 1.0f;
                if (overlayBloqueado != null) overlayBloqueado.SetActive(false);
                if (overlayConcluido != null) overlayConcluido.SetActive(false);
                break;

            case EstadoNoCaminho.Futuro:
                _botao.interactable = false;
                if (_canvasGroup != null) _canvasGroup.alpha = 0.4f;
                if (overlayBloqueado != null) overlayBloqueado.SetActive(true);
                if (overlayConcluido != null) overlayConcluido.SetActive(false);
                _escalaAlvo = _escalaOriginal;
                break;

            case EstadoNoCaminho.Concluido:
                _botao.interactable = false;
                if (_canvasGroup != null) _canvasGroup.alpha = 0.5f;
                if (overlayBloqueado != null) overlayBloqueado.SetActive(false);
                if (overlayConcluido != null) overlayConcluido.SetActive(true);
                _escalaAlvo = _escalaOriginal;
                break;
        }
    }

    private void OnNodeClicked()
    {
        if (EstadoAtual != EstadoNoCaminho.Disponivel) return;
        if (_caminhoManager == null) return;
        if (mapData == null)
        {
            Debug.LogWarning($"[PathNodeUI] Nó '{gameObject.name}' não tem MapData atribuído!");
            return;
        }
        _caminhoManager.AbrirPainelMapa(mapData);
    }

    public void OnPointerEnter(PointerEventData eventData)
    {
        if (EstadoAtual != EstadoNoCaminho.Disponivel) return;
        _escalaAlvo = _escalaOriginal * escalaHover;
    }

    public void OnPointerExit(PointerEventData eventData)
    {
        _escalaAlvo = _escalaOriginal;
    }

    private void OnDestroy()
    {
        if (_botao != null)
            _botao.onClick.RemoveListener(OnNodeClicked);
    }
}
