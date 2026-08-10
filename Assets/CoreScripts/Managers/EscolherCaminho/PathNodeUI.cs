using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// Componente colocado em cada nó clicável da tela de seleção de caminho.
/// Ao clicar, notifica o CaminhoManager para exibir o painel deste mapa.
/// </summary>
[RequireComponent(typeof(Button))]
public class PathNodeUI : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
{
    [Header("Dados do Mapa")]
    [Tooltip("ScriptableObject com os dados do mapa representado por este nó")]
    public MapData mapData;

    [Header("Referências do Nó")]
    [Tooltip("Imagem do ícone deste nó (opcional — para efeito de hover)")]
    public Image iconeImagem;

    [Header("Animação de Hover")]
    [Tooltip("Escala ao passar o mouse sobre o nó")]
    public float escalaHover = 1.15f;
    [Tooltip("Velocidade da animação de escala")]
    public float velocidadeAnimacao = 8f;

    // Referências internas
    private CaminhoManager _caminhoManager;
    private Vector3 _escalaOriginal;
    private Vector3 _escalaAlvo;
    private Button _botao;

    private void Awake()
    {
        _botao = GetComponent<Button>();
        _escalaOriginal = transform.localScale;
        _escalaAlvo = _escalaOriginal;
    }

    private void Start()
    {
        // Busca o CaminhoManager na cena
        _caminhoManager = FindObjectOfType<CaminhoManager>();

        if (_caminhoManager == null)
            Debug.LogWarning($"[PathNodeUI] CaminhoManager não encontrado na cena! Nó: {gameObject.name}");

        // Registra o click
        _botao.onClick.AddListener(OnNodeClicked);
    }

    private void Update()
    {
        // Animação suave de escala (hover)
        transform.localScale = Vector3.Lerp(transform.localScale, _escalaAlvo, Time.deltaTime * velocidadeAnimacao);
    }

    private void OnNodeClicked()
    {
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
