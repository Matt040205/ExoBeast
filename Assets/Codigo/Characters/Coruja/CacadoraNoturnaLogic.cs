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

    [Header("Juice Configs")]
    [SerializeField] private CameraShakeConfig ultimateShake = new CameraShakeConfig(5f, 5f, 0.6f);

    private NetworkVariable<float> netDamage = new NetworkVariable<float>();
    private NetworkVariable<float> netRange = new NetworkVariable<float>();
    private NetworkVariable<float> netWidth = new NetworkVariable<float>();
    private NetworkVariable<NetworkObjectReference> netCaster = new NetworkVariable<NetworkObjectReference>();

    private GameObject caster;
    private LayerMask visualRaycastMask;
    private Animator anim;
    private bool hasAppliedBeamDamage;
    private bool hasShownBeamVisual;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        LayerMask enemyLayer = LayerMask.GetMask("Enemy");
        LayerMask playerLayer = LayerMask.GetMask("Player");
        visualRaycastMask = ~(enemyLayer | playerLayer);

        if (effectParticles != null)
            effectParticles.Play();

        // O caster pode chegar antes ou depois do OnNetworkSpawn dependendo do timing de rede.
        // Registramos o callback para ambos os casos.
        netCaster.OnValueChanged += OnCasterAssigned;

        // Tentar setup imediato (funciona no servidor, onde StartUltimateEffect() jah rodou)
        if (netCaster.Value.TryGet(out NetworkObject casterNO))
            SetupCaster(casterNO);
    }

    private void OnCasterAssigned(NetworkObjectReference oldVal, NetworkObjectReference newVal)
    {
        if (newVal.TryGet(out NetworkObject casterNO))
            SetupCaster(casterNO);
    }

    private void SetupCaster(NetworkObject casterNO)
    {
        this.caster = casterNO.gameObject;
        this.anim = caster.GetComponentInChildren<Animator>();

        // BUG FIX (Bug 2 - 7 Maio 2026): proxy.magiaAtualDaCacadora precisa ser setado em TODOS os
        // clientes, nao so owner+server. O Animator dispara AnimEvent_FireBeam via SendMessage
        // (animation event chamando o metodo no proxy), entao o proxy precisa ter referencia para
        // ESTA instancia da CacadoraNoturnaLogic em qualquer cliente que vai ver o beam visual.
        // Antes: clientes nao-owner viam a animacao mas ShowBeamVisual nunca rodava.
        AnimationEventProxy proxy = caster.GetComponentInChildren<AnimationEventProxy>();
        if (proxy != null)
        {
            proxy.magiaAtualDaCacadora = this;
        }

        // Servidor dispara o trigger via NetworkAnimator — replica para todos os clientes
        if (IsServer && anim != null)
        {
            var networkAnimator = caster.GetComponentInChildren<NetworkAnimator>();
            if (networkAnimator != null) networkAnimator.SetTrigger("CacadoraUltimate");
            else anim.SetTrigger("CacadoraUltimate");
        }
    }

    public override void OnNetworkDespawn()
    {
        netCaster.OnValueChanged -= OnCasterAssigned;
        base.OnNetworkDespawn();
    }

    public void StartUltimateEffect(GameObject caster, float damage, float range, float width)
    {
        if (!IsServer) return;

        this.caster = caster;
        netDamage.Value = damage;
        netRange.Value = range;
        netWidth.Value = width;
        netCaster.Value = new NetworkObjectReference(caster.GetComponent<NetworkObject>());

        StartCoroutine(ServerForceBeamVisualFallback());
        StartCoroutine(ServerDespawnCoroutine());
    }

    private IEnumerator ServerForceBeamVisualFallback()
    {
        yield return null;
        ForceBeamVisualClientRpc();
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
        TryApplyBeamDamageOnce();
        TryShowBeamVisualLocal();
    }

    [ClientRpc]
    private void ForceBeamVisualClientRpc()
    {
        TryShowBeamVisualLocal();
    }

    private void TryApplyBeamDamageOnce()
    {
        if (!IsServer || hasAppliedBeamDamage)
            return;

        hasAppliedBeamDamage = true;
        ApplyBeamDamage();
    }

    private void TryShowBeamVisualLocal()
    {
        if (!IsClient || beamVisualPrefab == null || hasShownBeamVisual)
            return;

        hasShownBeamVisual = true;

        if (netCaster.Value.TryGet(out NetworkObject casterNO) && casterNO.IsOwner)
            JuiceEvents.OnCameraShake?.Invoke(transform.forward, ultimateShake.amplitude, ultimateShake.frequency, ultimateShake.duration);

        StartCoroutine(ShowBeamVisual());
    }

    private IEnumerator ShowBeamVisual()
    {
        Vector3 startPoint = transform.position;
        Vector3 direction = transform.forward;

        // Fallback: se netRange ainda não replicou no cliente, usa 100m como padrão
        float beamDistance = netRange.Value > 0.1f ? netRange.Value : 100f;

        Debug.Log($"[ArrowUlt] ShowBeamVisual INICIO - startPoint:{startPoint}, direction:{direction}, beamDistance:{beamDistance}, visualDuration:{visualDuration}");

        RaycastHit groundHit;
        if (Physics.Raycast(startPoint, direction, out groundHit, beamDistance, visualRaycastMask))
        {
            beamDistance = groundHit.distance;
            Debug.Log($"[ArrowUlt] Raycast bateu em algo a {beamDistance}m: {groundHit.collider.name}");
        }

        GameObject visual = Instantiate(beamVisualPrefab, startPoint, transform.rotation);

        LineRenderer line = visual.GetComponent<LineRenderer>();
        bool isLineRendererMode = (line != null);

        Debug.Log($"[ArrowUlt] isLineRendererMode={isLineRendererMode}, beamDistance final={beamDistance}");

        if (isLineRendererMode)
        {
            // Modo LineRenderer (beam antigo): prende ao pai e desenha a linha
            visual.transform.SetParent(this.transform);
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.forward * beamDistance);
            line.startWidth = netWidth.Value;
            line.endWidth = netWidth.Value;

            // Fade out do LineRenderer
            float elapsedTime = 0f;
            Material lineMaterial = line.material;
            Color originalColor = Color.white;
            if (lineMaterial != null && lineMaterial.HasColor("_Color"))
                originalColor = lineMaterial.color;

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
        else
        {
            // Modo VFX (flecha voadora): adiciona componente de voo e deixa ele cuidar do movimento
            float speed = beamDistance / Mathf.Max(visualDuration, 0.1f);
            Debug.Log($"[ArrowUlt] Modo VFX - speed={speed} m/s, vai voar por {visualDuration}s");

            ArrowMover mover = visual.AddComponent<ArrowMover>();
            mover.flyDirection = direction;
            mover.flySpeed = speed;
            mover.lifetime = visualDuration;
        }
    }

    private void ApplyBeamDamage()
    {
        if (!IsServer) return;

        LayerMask enemyLayer = LayerMask.GetMask("Enemy");
        Vector3 startPoint = transform.position;
        Vector3 direction = transform.forward;

        RaycastHit[] inimigosAcertados = Physics.SphereCastAll(startPoint, netWidth.Value, direction, netRange.Value, enemyLayer);
        HashSet<EnemyHealthSystem> damagedEnemies = new HashSet<EnemyHealthSystem>();

        foreach (var hit in inimigosAcertados)
        {
            EnemyHealthSystem vidaInimigo = hit.collider.GetComponentInParent<EnemyHealthSystem>();
            if (vidaInimigo != null && damagedEnemies.Add(vidaInimigo))
            {
                vidaInimigo.TakeDamage(netDamage.Value, 0f, false);
            }
        }
    }
}

/// <summary>
/// Componente simples que faz um GameObject voar em linha reta no Update.
/// Adicionado em runtime pela CacadoraNoturnaLogic para mover o VFX da flecha.
/// </summary>
public class ArrowMover : MonoBehaviour
{
    [HideInInspector] public Vector3 flyDirection;
    [HideInInspector] public float flySpeed;
    [HideInInspector] public float lifetime = 2f;

    private float timer = 0f;

    void Update()
    {
        timer += Time.deltaTime;
        if (timer >= lifetime)
        {
            Destroy(gameObject);
            return;
        }

        transform.position += flyDirection * flySpeed * Time.deltaTime;
    }
}
