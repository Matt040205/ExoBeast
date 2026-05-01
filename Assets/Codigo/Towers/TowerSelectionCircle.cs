using UnityEngine;

public class TowerSelectionCircle : MonoBehaviour
{
    [Header("Referencias Visuais")]
    [Tooltip("O GameObject do circulo (o mesh/quad) que muda de cor.")]
    public GameObject circleVisual;

    [Tooltip("Material para 'cor destacada' (quando o rato esta perto).")]
    public Material highlightMaterial;

    private Renderer circleRenderer;
    private Material defaultMaterial; // Guarda o material original

    void Start()
    {
        if (circleVisual == null)
        {
            Debug.LogError($"[TowerSelectionCircle] O 'circleVisual' nao foi definido no Inspector da torre {gameObject.name}");
            this.enabled = false;
            return;
        }

        circleRenderer = circleVisual.GetComponent<Renderer>();
        if (circleRenderer != null)
        {
            defaultMaterial = circleRenderer.material; // Guarda o material original
        }

        // Comeca desligado, como voce pediu
        circleVisual.SetActive(false);
    }

    public void Highlight()
    {
        if (circleRenderer != null && highlightMaterial != null)
        {
            circleVisual.SetActive(true);
            circleRenderer.material = highlightMaterial;
        }

        TowerRangeIndicator rangeInd = GetComponent<TowerRangeIndicator>();
        if (rangeInd == null) rangeInd = GetComponentInChildren<TowerRangeIndicator>();
        if (rangeInd != null) rangeInd.ShowRange(true);
    }

    public void Unhighlight()
    {
        if (circleRenderer != null && defaultMaterial != null)
        {
            circleRenderer.material = defaultMaterial;
            circleVisual.SetActive(false);
        }

        TowerRangeIndicator rangeInd = GetComponent<TowerRangeIndicator>();
        if (rangeInd == null) rangeInd = GetComponentInChildren<TowerRangeIndicator>();
        if (rangeInd != null) rangeInd.ShowRange(false);
    }
}
