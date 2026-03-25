using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;

/// <summary>
/// ── CacadoraNoturnaLogic ─────────────────────────────────
/// Spawned NetworkObject that fires a damage beam along the owl's forward axis.
///
///  ▸ Server spawns, sets NetworkVariables, then despawns after the animation window
///  ▸ AnimEvent_FireBeam called by Animator on all clients (synced via NetworkAnimator)
///  ▸ Server applies beam damage; all clients render the visual independently
/// ─────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class CacadoraNoturnaLogic : NetworkBehaviour
{
    public ParticleSystem effectParticles;
    public GameObject beamVisualPrefab;
    public float visualDuration = 0.5f;

    private NetworkVariable<float> netDamage = new NetworkVariable<float>();
    private NetworkVariable<float> netRange = new NetworkVariable<float>();
    private NetworkVariable<float> netWidth = new NetworkVariable<float>();
    private NetworkVariable<NetworkObjectReference> netCaster = new NetworkVariable<NetworkObjectReference>();

    private GameObject caster;
    private LayerMask visualRaycastMask;
    private Animator anim;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        LayerMask enemyLayer = LayerMask.GetMask("Enemy");
        LayerMask playerLayer = LayerMask.GetMask("Player");
        visualRaycastMask = ~(enemyLayer | playerLayer);

        if (netCaster.Value.TryGet(out NetworkObject casterNO))
        {
            this.caster = casterNO.gameObject;
            this.anim = caster.GetComponentInChildren<Animator>();

            AnimationEventProxy proxy = caster.GetComponentInChildren<AnimationEventProxy>();
            if (proxy != null)
            {
                // Only the owning client and server register for the anim event to avoid duplicate beam fire
                if (casterNO.IsOwner || IsServer)
                {
                    proxy.magiaAtualDaCacadora = this;
                }
            }
        }

        if (effectParticles != null)
        {
            effectParticles.Play();
        }

        // Server drives the animation trigger — NetworkAnimator replicates it to all clients
        if (IsServer && anim != null)
        {
            var networkAnimator = caster.GetComponentInChildren<NetworkAnimator>();
            if (networkAnimator != null) networkAnimator.SetTrigger("CacadoraUltimate");
            else anim.SetTrigger("CacadoraUltimate");
        }
    }

    public void StartUltimateEffect(GameObject caster, float damage, float range, float width)
    {
        if (!IsServer) return;

        this.caster = caster;
        netDamage.Value = damage;
        netRange.Value = range;
        netWidth.Value = width;
        netCaster.Value = new NetworkObjectReference(caster.GetComponent<NetworkObject>());

        StartCoroutine(ServerDespawnCoroutine());
    }

    private IEnumerator ServerDespawnCoroutine()
    {
        yield return new WaitForSeconds(visualDuration + 3.0f);
        if (NetworkObject.IsSpawned)
        {
            NetworkObject.Despawn();
        }
    }

    public void AnimEvent_FireBeam()
    {
        // Called by Animator on all clients because animations are synced via NetworkAnimator
        if (IsServer)
        {
            ApplyBeamDamage();
        }

        if (IsClient && beamVisualPrefab != null)
        {
            StartCoroutine(ShowBeamVisual());
        }
    }

    private IEnumerator ShowBeamVisual()
    {
        Vector3 startPoint = transform.position;
        Vector3 direction = transform.forward;
        float beamDistance = netRange.Value;

        RaycastHit groundHit;
        if (Physics.Raycast(startPoint, direction, out groundHit, netRange.Value, visualRaycastMask))
        {
            beamDistance = groundHit.distance;
        }

        GameObject visual = Instantiate(beamVisualPrefab, startPoint, transform.rotation);
        visual.transform.SetParent(this.transform);

        LineRenderer line = visual.GetComponent<LineRenderer>();
        if (line != null)
        {
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.forward * beamDistance);
            line.startWidth = netWidth.Value;
            line.endWidth = netWidth.Value;
        }

        float elapsedTime = 0f;
        Material lineMaterial = line?.material;
        Color originalColor = Color.white;
        if (lineMaterial != null && lineMaterial.HasColor("_Color"))
        {
            originalColor = lineMaterial.color;
        }

        while (elapsedTime < visualDuration)
        {
            elapsedTime += Time.deltaTime;
            float progress = elapsedTime / visualDuration;
            if (lineMaterial != null)
            {
                originalColor.a = 1f - progress;
                lineMaterial.color = originalColor;
            }
            yield return null;
        }

        if (visual != null) Destroy(visual);
    }

    private void ApplyBeamDamage()
    {
        if (!IsServer) return;

        LayerMask enemyLayer = LayerMask.GetMask("Enemy");
        Vector3 startPoint = transform.position;
        Vector3 direction = transform.forward;

        RaycastHit[] inimigosAcertados = Physics.SphereCastAll(startPoint, netWidth.Value, direction, netRange.Value, enemyLayer);

        foreach (var hit in inimigosAcertados)
        {
            EnemyHealthSystem vidaInimigo = hit.collider.GetComponent<EnemyHealthSystem>();
            if (vidaInimigo != null)
            {
                vidaInimigo.TakeDamage(netDamage.Value, 0f, false);
            }
        }
    }
}
