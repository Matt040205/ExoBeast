using UnityEngine;

public class TowerSelectionCircle : MonoBehaviour
{
    [Header("Referências Visuais")]
    [Tooltip("O GameObject do círculo (o mesh/quad) que muda de cor.")]
    public GameObject circleVisual;

    [Tooltip("Material para 'cor destacada' (quando o rato está perto).")]
    public Material highlightMaterial;

    private Renderer circleRenderer;
    private Material defaultMaterial; // Guarda o material original

    void Start()
    {
        if (circleVisual == null)
        {
            Debug.LogError($"[TowerSelectionCircle] O 'circleVisual' não foi definido no Inspector da torre {gameObject.name}");
            this.enabled = false;
            return;
        }

        circleRenderer = circleVisual.GetComponent<Renderer>();
        if (circleRenderer != null)
        {
            defaultMaterial = circleRenderer.material; // Guarda o material original
        }

        // Começa desligado, como você pediu
        circleVisual.SetActive(false);
    }

    public void Highlight()
    {
        if (circleRenderer == null || highlightMaterial == null) return;

        circleVisual.SetActive(true);
        circleRenderer.material = highlightMaterial;
    }

    public void Unhighlight()
    {
        if (circleRenderer == null || defaultMaterial == null) return;

        circleRenderer.material = defaultMaterial;
        circleVisual.SetActive(false);
    }
}