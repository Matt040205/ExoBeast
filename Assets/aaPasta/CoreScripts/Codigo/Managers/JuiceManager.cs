using UnityEngine;
using System.Collections;

/// <summary>
/// ── JuiceManager ───────────────────────────────────────
/// Gerencia efeitos de ''Game Feel'', como Hit-Stops temporários.
/// Utiliza Singleton para alcance global.
/// ───────────────────────────────────────────────────────
/// </summary>
public class JuiceManager : MonoBehaviour
{
    public static JuiceManager Instance { get; private set; }

    private bool isHitStopping = false;
    public float HitStopTime = 0.05f;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
    }

    /// <summary>
    /// Congela parcialmente o tempo do jogo para dar impacto aos golpes.
    /// Protegido contra acumulação (spam) para não travar o jogo.
    /// </summary>
    public void HitStop(float duration)
    {
        if (isHitStopping) return;
        
        StartCoroutine(HitStopRoutine(duration));
    }

    private IEnumerator HitStopRoutine(float duration)
    {
        isHitStopping = true;

        // Reduz a escala temporal em vez de zeros absolutos para não quebrar a lógica de rede
        Time.timeScale = HitStopTime;

        // Aguarda em tempo real (ignora a dilatação do TimeScale)
        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = 1f;
        isHitStopping = false;
    }
}
