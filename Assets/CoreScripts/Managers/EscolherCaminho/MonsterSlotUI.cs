using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Componente colocado em cada ícone/botão de monstro dentro do grid do PainelMonstros.
/// Ao clicar, notifica o CaminhoManager para exibir a aba de detalhes deste inimigo.
/// </summary>
public class MonsterSlotUI : MonoBehaviour
{
    [Tooltip("Dados do inimigo representado por este slot (EnemyDataSO do projeto)")]
    public EnemyDataSO enemyData;

    [Tooltip("Image que exibe o ícone do inimigo")]
    public Image iconeImagem;

    private CaminhoManager _caminhoManager;
    private Button _botao;

    private void Awake()
    {
        GarantirReferencias();
    }

    private void Start()
    {
        if (_caminhoManager == null)
            _caminhoManager = FindObjectOfType<CaminhoManager>();

        if (_caminhoManager == null)
            Debug.LogWarning($"[MonsterSlotUI] CaminhoManager não encontrado na cena! Slot: {gameObject.name}");

        AplicarIcone();
    }

    private void GarantirReferencias()
    {
        if (_botao == null)
            _botao = GetComponent<Button>();

        if (_botao == null)
            _botao = GetComponentInChildren<Button>(true);

        if (iconeImagem == null)
            iconeImagem = GetComponent<Image>();

        if (iconeImagem == null)
            iconeImagem = GetComponentInChildren<Image>(true);

        if (_botao != null)
        {
            _botao.onClick.RemoveListener(OnSlotClicked);
            _botao.onClick.AddListener(OnSlotClicked);
        }
        else
        {
            Debug.LogWarning($"[MonsterSlotUI] Nenhum componente Button foi encontrado em '{gameObject.name}' ou seus filhos!");
        }
    }

    /// <summary>
    /// Configura este slot com um EnemyDataSO — chamado pelo CaminhoManager ao popular o grid.
    /// </summary>
    public void Configurar(EnemyDataSO dados, CaminhoManager manager)
    {
        enemyData = dados;
        _caminhoManager = manager;

        GarantirReferencias();
        AplicarIcone();
    }

    private void AplicarIcone()
    {
        if (iconeImagem != null)
        {
            iconeImagem.enabled = true;
            if (enemyData != null && enemyData.icone != null)
            {
                iconeImagem.sprite = enemyData.icone;
            }
        }
    }

    private void OnSlotClicked()
    {
        Debug.Log($"[MonsterSlotUI] Slot clicado! Monstro: {(enemyData != null ? enemyData.name : "null")}");

        if (_caminhoManager == null)
            _caminhoManager = FindObjectOfType<CaminhoManager>();

        if (_caminhoManager == null)
        {
            Debug.LogWarning("[MonsterSlotUI] CaminhoManager não encontrado ao clicar no slot!");
            return;
        }

        if (enemyData == null)
        {
            Debug.LogWarning("[MonsterSlotUI] enemyData é null ao clicar no slot!");
            return;
        }

        _caminhoManager.AbrirAbaDetalhesMonstro(enemyData);
    }

    private void OnDestroy()
    {
        if (_botao != null)
            _botao.onClick.RemoveListener(OnSlotClicked);
    }
}
