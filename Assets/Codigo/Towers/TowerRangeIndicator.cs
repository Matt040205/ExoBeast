using UnityEngine;

[RequireComponent(typeof(Collider))]
public class TowerRangeIndicator : MonoBehaviour
{
    [Header("Referências")]
    [Tooltip("O GameObject que contém o visual do range (disco/cilindro do chão)")]
    public GameObject rangeVisual;
    
    [Tooltip("Referência ao TowerController da torre. Se vazio, o script tentará encontrar sozinho.")]
    public TowerController towerController;

    private bool isMouseOver = false;

    private void Awake()
    {
        // Tenta achar o TowerController automaticamente caso não tenha sido preenchido no Inspector
        if (towerController == null)
        {
            towerController = GetComponent<TowerController>();
            if (towerController == null) towerController = GetComponentInParent<TowerController>();
            if (towerController == null) towerController = GetComponentInChildren<TowerController>();
        }

        if (rangeVisual != null)
        {
            rangeVisual.SetActive(false); // Oculta o disco inicialmente
        }
    }

    private void OnMouseEnter()
    {
        isMouseOver = true;
        UpdateRangeVisual();
    }

    private void OnMouseExit()
    {
        isMouseOver = false;
        UpdateRangeVisual();
    }

    private void Update()
    {
        // Se o mouse já estiver em cima da torre e o jogador pressionar a tecla B para 
        // entrar ou sair do Modo Aéreo, atualizamos o visual em tempo real.
        if (isMouseOver)
        {
            UpdateRangeVisual();
        }
    }

    private void UpdateRangeVisual()
    {
        if (rangeVisual == null || towerController == null) return;

        // Regra de Ouro: Só mostra se estiver passando o mouse E o Modo Aéreo (isBuildingMode) for verdadeiro
        bool shouldShow = isMouseOver && BuildManager.isBuildingMode;

        if (shouldShow)
        {
            // O Range representa o raio. Para a escala do cilindro/esfera, precisamos do diâmetro (Raio * 2)
            float diameter = towerController.CurrentRange * 2f;

            // Altera apenas os eixos X e Z, preservando o Y original
            Vector3 newScale = rangeVisual.transform.localScale;
            newScale.x = diameter;
            newScale.z = diameter;
            rangeVisual.transform.localScale = newScale;

            if (!rangeVisual.activeSelf)
            {
                rangeVisual.SetActive(true);
            }
        }
        else
        {
            if (rangeVisual.activeSelf)
            {
                rangeVisual.SetActive(false);
            }
        }
    }
}
