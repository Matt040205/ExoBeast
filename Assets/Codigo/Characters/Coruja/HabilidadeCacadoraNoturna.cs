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
        if (logicVisualPrefab == null || quemUsou == null)
            return false;

        bool isNetworkSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkSession)
        {
            if (!NetworkManager.Singleton.IsServer)
                return false;

            GameObject logicObject = Object.Instantiate(
                logicVisualPrefab,
                quemUsou.transform.position,
                AbilityAimUtility.ResolveAimRotation(quemUsou));

            if (!logicObject.TryGetComponent(out NetworkObject netObj) ||
                !logicObject.TryGetComponent(out CacadoraNoturnaLogic logic))
            {
                Debug.LogError("[HabilidadeCacadoraNoturna] logicVisualPrefab precisa apontar para CacadoraNoturnaLogic.prefab com NetworkObject.");
                Object.Destroy(logicObject);
                return false;
            }

            netObj.Spawn();
            logic.StartUltimateEffect(quemUsou, damage, range, width, delayTiro);
            return true;
        }

        if (logicVisualPrefab.GetComponent<CacadoraNoturnaLogic>() != null)
        {
            GameObject logicObject = Object.Instantiate(
                logicVisualPrefab,
                quemUsou.transform.position,
                AbilityAimUtility.ResolveAimRotation(quemUsou));

            CacadoraNoturnaLogic logic = logicObject.GetComponent<CacadoraNoturnaLogic>();
            if (logic != null)
                logic.StartOfflineUltimateEffect(quemUsou, damage, range, width, delayTiro);

            return true;
        }

        MonoBehaviour mb = quemUsou.GetComponent<MonoBehaviour>();
        if (mb != null)
            mb.StartCoroutine(DisparoDelayFallbackCoroutine(quemUsou));

        return mb != null;
    }

    private IEnumerator DisparoDelayFallbackCoroutine(GameObject quemUsou)
    {
        yield return new WaitForSeconds(delayTiro);

        ResolveBeamPose(quemUsou, out Vector3 startPoint, out Vector3 direction, out Quaternion spawnRotation);

        GameObject vfx = Object.Instantiate(logicVisualPrefab, startPoint, spawnRotation);
        ParticleSystem[] particles = vfx.GetComponentsInChildren<ParticleSystem>();
        foreach (var p in particles) p.Play();
        Object.Destroy(vfx, 4.0f);

        ApplyBeamDamage(startPoint, direction);
    }

    private void ResolveBeamPose(GameObject quemUsou, out Vector3 startPoint, out Vector3 direction, out Quaternion spawnRotation)
    {
        Transform firePoint = quemUsou.transform;
        PlayerShooting shootingScript = quemUsou.GetComponent<PlayerShooting>();
        if (shootingScript != null && shootingScript.firePoint != null)
            firePoint = shootingScript.firePoint;

        startPoint = firePoint.position;
        direction = AbilityAimUtility.ResolveAimForward(quemUsou);
        if (direction.sqrMagnitude <= 0.001f)
            direction = quemUsou.transform.forward;

        direction.Normalize();
        spawnRotation = Quaternion.LookRotation(direction, Vector3.up);
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
