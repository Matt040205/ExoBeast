using UnityEngine;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// ── HabilidadeCacadoraNoturna ────────────────────────────
/// Suprema da Coruja: dispara um beam que percorre todo o mapa,
/// causando dano massivo em todos os inimigos no caminho.
/// ─────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Cacadora Noturna", menuName = "ExoBeasts/Personagens/Coruja/Habilidade/Cacadora Noturna")]
public class HabilidadeCacadoraNoturna : Ability
{
    [Header("Configuracoes da Habilidade")]
    public float damage = 300f;
    public float range = 100f;
    public float width = 3f;

    [Header("Visual & Feedback")]
    [Tooltip("Arraste o Prefab que contém os efeitos de partícula originais ou o CacadoraNoturnaLogic")]
    public GameObject logicVisualPrefab;

    [Tooltip("Tempo (em segundos) que a script espera a animação rodar antes de atirar o raio/dano.")]
    public float delayTiro = 1.0f;

    public override bool Activate(GameObject quemUsou)
    {
        Debug.Log("[CacadoraNoturna] Activate() chamado!");

        PlayerShooting shootingScript = quemUsou.GetComponent<PlayerShooting>();
        PlayerMovement movementScript = quemUsou.GetComponent<PlayerMovement>();
        Animator anim = quemUsou.GetComponentInChildren<Animator>();

        Transform modelPivot = (movementScript != null) ? movementScript.GetModelPivot() : quemUsou.transform;
        Transform firePoint = (shootingScript != null && shootingScript.firePoint != null)
            ? shootingScript.firePoint
            : quemUsou.transform;

        Vector3 startPoint = firePoint.position;
        Vector3 direction = modelPivot.forward;

        Debug.Log($"[CacadoraNoturna] Posição: {startPoint}, Direção: {direction}, Range: {range}, Dano: {damage}");

        // Toca animação da suprema (Animação de preparar o arco começa agora!)
        if (anim != null)
        {
            anim.SetTrigger("CacadoraUltimate");
        }

        // Delega o disparo e o dano para acontecer APÓS o tempo de delay da animação
        MonoBehaviour mb = quemUsou.GetComponent<MonoBehaviour>();
        if (mb != null)
        {
            mb.StartCoroutine(DisparoDelayCoroutine(startPoint, direction));
        }

        return true;
    }

    private System.Collections.IEnumerator DisparoDelayCoroutine(Vector3 startPoint, Vector3 direction)
    {
        // Espera a personagem terminar a pose da animação e "soltar" o tiro
        yield return new WaitForSeconds(delayTiro);

        // Instancia o prefab de Efeitos Visuais (Partículas, LineRenderers originais)
        if (logicVisualPrefab != null)
        {
            GameObject vfx = Object.Instantiate(logicVisualPrefab, startPoint, Quaternion.LookRotation(direction));
            
            // Toca eventuais partículas configuradas no topo do prefab
            ParticleSystem[] particles = vfx.GetComponentsInChildren<ParticleSystem>();
            foreach (var p in particles) p.Play();

            // Destroi o visual de lógica depois da duração
            Object.Destroy(vfx, 4.0f);
        }

        // Aplica dano via SphereCast inline (já atinge todos na linha de forma confiável local/rede)
        ApplyBeamDamage(startPoint, direction);
    }

    private void ApplyBeamDamage(Vector3 startPoint, Vector3 direction)
    {
        LayerMask enemyLayer = LayerMask.GetMask("Enemy");

        RaycastHit[] hits = Physics.SphereCastAll(startPoint, width, direction, range, enemyLayer);

        Debug.Log($"[CacadoraNoturna] SphereCast atingiu {hits.Length} alvos");

        foreach (var hit in hits)
        {
            EnemyHealthSystem health = hit.collider.GetComponent<EnemyHealthSystem>();
            if (health == null)
                health = hit.collider.GetComponentInParent<EnemyHealthSystem>();

            if (health != null)
            {
                health.TakeDamage(damage, 0f, false);
                Debug.Log($"[CacadoraNoturna] {hit.collider.name} recebeu {damage} de dano!");
            }
        }
    }
}
