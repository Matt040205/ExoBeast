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
        if (logicVisualPrefab == null) return false;

        PlayerShooting shootingScript = quemUsou.GetComponent<PlayerShooting>();
        PlayerMovement movementScript = quemUsou.GetComponent<PlayerMovement>();

        // modelPivot.rotation agora esta sincronizado via netModelYRot (fix BUG A)
        Transform modelPivot = (movementScript != null) ? movementScript.GetModelPivot() : quemUsou.transform;
        Transform firePoint = (shootingScript != null && shootingScript.firePoint != null)
            ? shootingScript.firePoint
            : quemUsou.transform;

        Vector3 startPoint = firePoint.position;
        Quaternion spawnRotation = modelPivot.rotation;

        MonoBehaviour mb = quemUsou.GetComponent<MonoBehaviour>();
        if (mb != null)
            mb.StartCoroutine(DisparoDelayCoroutine(quemUsou, startPoint, spawnRotation));

        return true;
    }

    private System.Collections.IEnumerator DisparoDelayCoroutine(GameObject quemUsou, Vector3 startPoint, Quaternion spawnRotation)
    {
        yield return new WaitForSeconds(delayTiro);

        GameObject vfx = Object.Instantiate(logicVisualPrefab, startPoint, spawnRotation);

        if (vfx.TryGetComponent<NetworkObject>(out var netObj))
        {
            // Spawnar em rede: todos os clientes verao o VFX
            netObj.Spawn();
            CacadoraNoturnaLogic logic = vfx.GetComponent<CacadoraNoturnaLogic>();
            if (logic != null)
                logic.StartUltimateEffect(quemUsou, damage, range, width);
        }
        else
        {
            // Fallback sem NGO (singleplayer)
            ParticleSystem[] particles = vfx.GetComponentsInChildren<ParticleSystem>();
            foreach (var p in particles) p.Play();
            Object.Destroy(vfx, 4.0f);

            ApplyBeamDamage(startPoint, spawnRotation * Vector3.forward);
        }
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
