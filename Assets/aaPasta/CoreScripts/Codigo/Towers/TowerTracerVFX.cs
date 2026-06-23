using System.Collections;
using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class TowerTracerVFX : MonoBehaviour
{
    private LineRenderer lineRenderer;
    
    [Header("Configurações do Tracer")]
    [Tooltip("Tempo em segundos para o rastro desaparecer (Largura ir para 0)")]
    public float fadeDuration = 0.08f;

    private Coroutine currentFadeRoutine;
    private float initialWidthMultiplier;

    private void Awake()
    {
        lineRenderer = GetComponent<LineRenderer>();
        lineRenderer.enabled = false;
        
        // Salva a largura inicial configurada no Inspector para usarmos como base no fade
        initialWidthMultiplier = lineRenderer.widthMultiplier;
    }

    /// <summary>
    /// Ativa o rastro visual entre os pontos inicial e final especificados e inicia o Fade Out.
    /// </summary>
    public void DrawTracer(Vector3 startPoint, Vector3 endPoint)
    {
        // Interrompe qualquer animação de fade anterior se a torre atirar muito rápido
        if (currentFadeRoutine != null)
        {
            StopCoroutine(currentFadeRoutine);
        }

        // Define os pontos
        lineRenderer.SetPosition(0, startPoint);
        lineRenderer.SetPosition(1, endPoint);
        
        // Restaura a largura inicial caso o tiro anterior já tenha reduzido a largura a zero
        lineRenderer.widthMultiplier = initialWidthMultiplier;
        lineRenderer.enabled = true;

        // Inicia a redução gradual
        currentFadeRoutine = StartCoroutine(FadeOutRoutine());
    }

    private IEnumerator FadeOutRoutine()
    {
        float elapsedTime = 0f;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            
            // Calcula o progresso do fade de 0 a 1
            float progress = elapsedTime / fadeDuration;
            
            // Reduz a largura do Line Renderer de 100% (initialWidth) até 0%
            lineRenderer.widthMultiplier = Mathf.Lerp(initialWidthMultiplier, 0f, progress);

            yield return null; // Espera o próximo frame
        }

        // Garante que o valor termine exatamente em zero e desativa para poupar processamento
        lineRenderer.widthMultiplier = 0f;
        lineRenderer.enabled = false;
        currentFadeRoutine = null;
    }
}
