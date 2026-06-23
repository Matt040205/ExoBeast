using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>
/// ── PooledHealAuraVisuals ─────────────────────────────────
/// Controla os efeitos visuais do Prefab "HealAura" de forma
/// compatível com sistemas de pooling em ambientes multiplayer.
///
///  ▸ Deve ser anexado ao GameObject raiz "HealAura".
///  ▸ Filhos gerenciados: "Heal+" (ParticleSystem),
///    "HealFeixe" (VisualEffect), "Ponta_Magia" (pai),
///    "Glitter_Estrelas" (ParticleSystem, filho de Ponta_Magia).
///  ▸ Ativado pelo PoolingManager via OnSpawnFromPool().
///  ▸ Desativa-se automaticamente após a duração e sinaliza
///    ao PoolingManager via callback para retorno ao pool.
/// ─────────────────────────────────────────────────────────
/// </summary>
public class PooledHealAuraVisuals : MonoBehaviour
{
    // ── Referências aos filhos do Prefab ─────────────────────

    [Header("Filhos do HealAura")]
    [Tooltip("Particle System responsável pelos símbolos de '+' de cura.")]
    [SerializeField] private ParticleSystem healPlusParticles;       // Filho: "Heal+"

    [Tooltip("VFX Graph responsável pelo feixe/aurora de cura.")]
    [SerializeField] private VisualEffect healFeixeVFX;              // Filho: "HealFeixe"

    [Tooltip("Parent do Glitter_Estrelas. Não precisa de controle direto, mas é exposto para inspeção.")]
    [SerializeField] private Transform pontaMagia;                   // Filho: "Ponta_Magia"

    [Tooltip("Particle System de estrelas/glitter. Filho de Ponta_Magia.")]
    [SerializeField] private ParticleSystem glitterEstrelasParticles; // Neto: "Glitter_Estrelas"

    // ── Estado Interno ────────────────────────────────────────

    private Coroutine _durationCoroutine;

    /// <summary>
    /// Callback invocado quando o efeito termina, para que o PoolingManager
    /// devolva este objeto ao pool. Assinado externamente pelo manager.
    /// Recebe o próprio GameObject como parâmetro para facilitar a devolução.
    /// </summary>
    public System.Action<GameObject> OnReturnToPool;

    // ── Pool Interface ────────────────────────────────────────

    /// <summary>
    /// Chamado pelo PoolingManager para ativar o efeito visual.
    /// Parenteia o HealAura ao transform fornecido (ex: o jogador curado),
    /// reposiciona localmente e inicia todos os sistemas de partículas/VFX.
    /// </summary>
    /// <param name="parent">Transform ao qual o HealAura será parenteado (ex: Transform do jogador).</param>
    /// <param name="duration">Duração em segundos até o efeito ser automaticamente desativado e retornado ao pool.</param>
    public void OnSpawnFromPool(Transform parent, float duration)
    {
        // 1. Parentear e zerar posição/rotação locais
        transform.SetParent(parent, worldPositionStays: false);
        transform.localPosition = Vector3.zero;
        transform.localRotation = Quaternion.identity;

        // 2. Ativar o GameObject raiz (pode estar desativado no pool)
        gameObject.SetActive(true);

        // 3. Lançar todos os efeitos visuais
        PlayAllEffects();

        // 4. Iniciar corrotina de duração (cancela qualquer anterior)
        if (_durationCoroutine != null)
        {
            StopCoroutine(_durationCoroutine);
            _durationCoroutine = null;
        }
        _durationCoroutine = StartCoroutine(DurationRoutine(duration));
    }

    /// <summary>
    /// Chamado pelo PoolingManager antes de devolver ao pool (opcional),
    /// ou internamente quando a duração expira.
    /// Para todos os efeitos e remove o parentesco.
    /// </summary>
    public void OnReturnedToPool()
    {
        // Cancelar corrotina de duração pendente
        if (_durationCoroutine != null)
        {
            StopCoroutine(_durationCoroutine);
            _durationCoroutine = null;
        }

        StopAllEffects();

        // Desparenta para o pool reutilizar livremente
        transform.SetParent(null, worldPositionStays: false);
    }

    // ── Controle Interno de Efeitos ───────────────────────────

    private void PlayAllEffects()
    {
        // ParticleSystem: "Heal+"
        if (healPlusParticles != null)
        {
            healPlusParticles.Clear();
            healPlusParticles.Play();
        }

        // ParticleSystem: "Glitter_Estrelas"
        if (glitterEstrelasParticles != null)
        {
            glitterEstrelasParticles.Clear();
            glitterEstrelasParticles.Play();
        }

        // VFX Graph: "HealFeixe"
        if (healFeixeVFX != null)
        {
            healFeixeVFX.enabled = true;
            healFeixeVFX.Play();
        }
    }

    private void StopAllEffects()
    {
        // ParticleSystem: "Heal+" — stop sem forçar: deixa partículas
        // existentes terminarem naturalmente (ShurikenParticle safe)
        if (healPlusParticles != null)
        {
            healPlusParticles.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
        }

        // ParticleSystem: "Glitter_Estrelas"
        if (glitterEstrelasParticles != null)
        {
            glitterEstrelasParticles.Stop(withChildren: true, ParticleSystemStopBehavior.StopEmitting);
        }

        // VFX Graph: "HealFeixe"
        if (healFeixeVFX != null)
        {
            healFeixeVFX.Stop();
            healFeixeVFX.enabled = false;
        }
    }

    // ── Corrotina de Duração ──────────────────────────────────

    private IEnumerator DurationRoutine(float duration)
    {
        yield return new WaitForSeconds(duration);

        StopAllEffects();

        // Notificar o PoolingManager para retomar a posse do objeto
        OnReturnToPool?.Invoke(gameObject);

        _durationCoroutine = null;
    }

    // ── Ciclo de Vida (Limpeza de Segurança) ──────────────────

    private void OnDisable()
    {
        // Garante que corrotinas não sobrevivam ao desativamento do objeto,
        // evitando NullReferenceExceptions típicas de objetos poolados.
        if (_durationCoroutine != null)
        {
            StopCoroutine(_durationCoroutine);
            _durationCoroutine = null;
        }

        // Limpa a referência do callback para evitar retenção de memória
        // caso o manager que assinou seja destruído.
        OnReturnToPool = null;
    }

    // ── Auto-referenciamento no Editor ────────────────────────

#if UNITY_EDITOR
    /// <summary>
    /// Tenta encontrar automaticamente os filhos pelo nome no Editor.
    /// Útil ao adicionar o script ao Prefab pela primeira vez.
    /// </summary>
    [ContextMenu("Auto-Referenciar Filhos pelo Nome")]
    private void AutoBindChildren()
    {
        Transform healPlus = transform.Find("Heal+");
        if (healPlus != null)
            healPlusParticles = healPlus.GetComponent<ParticleSystem>();

        Transform healFeixe = transform.Find("HealFeixe");
        if (healFeixe != null)
            healFeixeVFX = healFeixe.GetComponent<VisualEffect>();

        pontaMagia = transform.Find("Ponta_Magia");

        if (pontaMagia != null)
        {
            Transform glitter = pontaMagia.Find("Glitter_Estrelas");
            if (glitter != null)
                glitterEstrelasParticles = glitter.GetComponent<ParticleSystem>();
        }

        UnityEditor.EditorUtility.SetDirty(this);
        Debug.Log("[PooledHealAuraVisuals] Auto-referenciamento concluído. Verifique os campos no Inspector.");
    }
#endif
}
