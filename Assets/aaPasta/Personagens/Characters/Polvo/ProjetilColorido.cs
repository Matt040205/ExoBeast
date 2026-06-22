using UnityEngine;

public class ProjetilColorido : MonoBehaviour
{
    [Header("Paleta de Cores")]
    [Tooltip("Lista de cores possíveis para o tiro.")]
    public Color[] coresPossiveis;

    [Header("Opcionais (Arrastar se tiver)")]
    public TrailRenderer rastroTrail; // Se o tiro tiver rastro (cauda)
    public Light luzDoTiro;           // Se o tiro tiver uma luzinha

    private Renderer _renderer;
    private MaterialPropertyBlock _propBlock;

    void Awake()
    {
        _renderer = GetComponent<Renderer>();
        _propBlock = new MaterialPropertyBlock();
    }

    // Usamos OnEnable porque seu jogo usa POOL. 
    // Assim, toda vez que o tiro sai da arma, ele roda isso de novo.
    void OnEnable()
    {
        if (coresPossiveis.Length == 0) return;

        // 1. Sorteia uma cor da lista
        Color corSorteada = coresPossiveis[Random.Range(0, coresPossiveis.Length)];

        // 2. Pinta o objeto 3D (Mesh) usando PropertyBlock (Mais leve para performance)
        if (_renderer != null)
        {
            _renderer.GetPropertyBlock(_propBlock);

            // Define a cor base
            _propBlock.SetColor("_BaseColor", corSorteada); // Para URP/HDRP
            _propBlock.SetColor("_Color", corSorteada);     // Para Built-in (Standard)

            // Se o material tiver EMISSION (Brilho), pintamos o brilho também
            _propBlock.SetColor("_EmissionColor", corSorteada * 2f); // *2f deixa o brilho mais forte

            _renderer.SetPropertyBlock(_propBlock);
        }

        // 3. Pinta o Rastro (Trail Renderer) se tiver
        if (rastroTrail != null)
        {
            // O Trail precisa de um gradiente para funcionar bem
            Gradient gradient = new Gradient();
            gradient.SetKeys(
                new GradientColorKey[] { new GradientColorKey(corSorteada, 0.0f), new GradientColorKey(corSorteada, 1.0f) },
                new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.0f, 1.0f) }
            );
            rastroTrail.colorGradient = gradient;
        }

        // 4. Pinta a Luz se tiver
        if (luzDoTiro != null)
        {
            luzDoTiro.color = corSorteada;
        }
    }
}