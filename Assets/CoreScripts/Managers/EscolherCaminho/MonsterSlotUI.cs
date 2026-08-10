using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Componente colocado em cada ícone/botão de monstro dentro do grid do PainelMonstros.
/// Ao clicar, notifica o CaminhoManager para exibir a aba de detalhes deste inimigo.
/// </summary>
[RequireComponent(typeof(UnityEngine.UI.Button))]
public class MonsterSlotUI : MonoBehaviour
{
    [Tooltip("Dados do inimigo representado por este slot (EnemyDataSO do projeto)")]
    public EnemyDataSO enemyData;

    [Tooltip("Image que exibe o ícone do inimigo")]
    public Image iconeImagem;

    private CaminhoManager _caminhoManager;
    private UnityEngine.UI.Button _botao;

    private void Awake()
    {
        _botao = GetComponent<UnityEngine.UI.Button>();
        if (iconeImagem == null)
            iconeImagem = GetComponent<Image>();
    }

    private void Start()
    {
        _caminhoManager = FindObjectOfType<CaminhoManager>();

        if (_caminhoManager == null)
            Debug.LogWarning($"[MonsterSlotUI] CaminhoManager não encontrado na cena! Slot: {gameObject.name}");

        _botao.onClick.AddListener(OnSlotClicked);
        AplicarIcone();
    }

    /// <summary>
    /// Configura este slot com um EnemyDataSO (chamado pelo CaminhoManager ao popular o grid).
    /// </summary>
    public void Configurar(EnemyDataSO dados, CaminhoManager manager)
    {
        enemyData = dados;
        _caminhoManager = manager;
        AplicarIcone();
    }

    private void AplicarIcone()
    {
        if (iconeImagem != null && enemyData != null && enemyData.icone != null)
            iconeImagem.sprite = enemyData.icone;
    }

    private void OnSlotClicked()
    {
        if (_caminhoManager == null || enemyData == null) return;
        _caminhoManager.AbrirAbaDetalhesMonstro(enemyData);
    }

    private void OnDestroy()
    {
        if (_botao != null)
            _botao.onClick.RemoveListener(OnSlotClicked);
    }
}
