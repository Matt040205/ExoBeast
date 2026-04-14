using UnityEngine;
using System.Collections;

public class SupportVFXController : MonoBehaviour
{
    [Header("Configurações de Cura (Aurora e +)")]
    [Tooltip("O Particle System que emite os símbolos de +")]
    [SerializeField] private ParticleSystem healParticles;
    [Tooltip("O Mesh Renderer do cilindro que possui o Shader Graph da Aurora")]
    [SerializeField] private Renderer auroraRenderer;
    [Tooltip("Tempo em segundos que a aura verde fica acesa após a cura")]
    [SerializeField] private float healVFXDuration = 2.0f;

    [Header("Configurações de Escudo (Bolha)")]
    [Tooltip("O GameObject da esfera do escudo")]
    [SerializeField] private GameObject shieldObject;
    [Tooltip("Tempo da animação do escudo inflando/desinflando")]
    [SerializeField] private float shieldPopDuration = 0.2f;

    // Referências para podermos cancelar as rotinas caso o jogador 
    // tome cura duas vezes seguidas muito rápido, evitando bugs visuais.
    private Coroutine activeHealCoroutine;
    private Coroutine activeShieldCoroutine;

    private void Start()
    {
        // Garante que tudo comece desligado quando o jogador nascer
        if (auroraRenderer != null) auroraRenderer.enabled = false;
        if (shieldObject != null) shieldObject.SetActive(false);
    }

    #region SISTEMA DE CURA
    /// <summary>
    /// Chama esta função localmente quando o servidor avisar que o jogador recuperou HP.
    /// </summary>
    public void TriggerHealVFX()
    {
        if (activeHealCoroutine != null)
        {
            StopCoroutine(activeHealCoroutine);
        }
        activeHealCoroutine = StartCoroutine(HealRoutine());
    }

    private IEnumerator HealRoutine()
    {
        // Liga o shader da aurora e dá o play nas partículas de "+"
        if (auroraRenderer != null) auroraRenderer.enabled = true;
        if (healParticles != null) healParticles.Play();

        // Espera o tempo definido
        yield return new WaitForSeconds(healVFXDuration);

        // Desliga a aurora e manda as partículas pararem de nascer
        // (As que já nasceram vão sumir suavemente por conta própria)
        if (auroraRenderer != null) auroraRenderer.enabled = false;
        if (healParticles != null) healParticles.Stop();

        activeHealCoroutine = null;
    }
    #endregion

    #region SISTEMA DE ESCUDO
    /// <summary>
    /// Chama esta função quando o jogador ganhar um buff de escudo protetor.
    /// </summary>
    public void EnableShield()
    {
        if (shieldObject == null) return;

        if (activeShieldCoroutine != null)
        {
            StopCoroutine(activeShieldCoroutine);
        }

        shieldObject.SetActive(true);
        // Animação de "pop": vai do tamanho 0 até o tamanho 1
        activeShieldCoroutine = StartCoroutine(ScaleShieldRoutine(Vector3.zero, Vector3.one));
    }

    /// <summary>
    /// Chama esta função quando o escudo quebrar ou o tempo do buff acabar.
    /// </summary>
    public void DisableShield()
    {
        if (shieldObject == null || !shieldObject.activeSelf) return;

        if (activeShieldCoroutine != null)
        {
            StopCoroutine(activeShieldCoroutine);
        }

        // Animação inversa: murcha do tamanho atual até 0 e depois desativa o objeto
        activeShieldCoroutine = StartCoroutine(ScaleShieldRoutine(shieldObject.transform.localScale, Vector3.zero, true));
    }

    private IEnumerator ScaleShieldRoutine(Vector3 startScale, Vector3 targetScale, bool disableAfter = false)
    {
        float elapsedTime = 0f;
        shieldObject.transform.localScale = startScale;

        // Interpola a escala para criar a animação suave (estilo anime juice)
        while (elapsedTime < shieldPopDuration)
        {
            shieldObject.transform.localScale = Vector3.Lerp(startScale, targetScale, elapsedTime / shieldPopDuration);
            elapsedTime += Time.deltaTime;
            yield return null; // Espera o próximo frame
        }

        // Garante que terminou no tamanho exato
        shieldObject.transform.localScale = targetScale;

        if (disableAfter)
        {
            shieldObject.SetActive(false);
        }

        activeShieldCoroutine = null;
    }
    #endregion
}