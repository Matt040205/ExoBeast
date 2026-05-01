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
        if (towerController == null)
        {
            towerController = GetComponent<TowerController>();
            if (towerController == null) towerController = GetComponentInParent<TowerController>();
            if (towerController == null) towerController = GetComponentInChildren<TowerController>();
        }

        if (rangeVisual != null)
        {
            rangeVisual.SetActive(false);
        }
    }

    public void ShowRange(bool show)
    {
        if (rangeVisual == null || towerController == null) return;

        if (show)
        {
            float diameter = towerController.CurrentRange * 2f;
            Vector3 newScale = rangeVisual.transform.localScale;
            newScale.x = diameter;
            newScale.z = diameter;
            rangeVisual.transform.localScale = newScale;
            rangeVisual.SetActive(true);
        }
        else
        {
            rangeVisual.SetActive(false);
        }
    }
}
