using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using Unity.Netcode.Components;

/// <summary>
/// Networked logic for the Owl ultimate. The server owns damage/timing and clients
/// receive an explicit visual payload so the beam does not depend on animation events.
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
    private UniversalCharacterAnimator universalAnimator;
    private bool hasAppliedBeamDamage;
    private bool hasShownBeamVisual;
    private bool hasBeamVisualPayload;
    private uint visualSequence;
    private uint lastShownVisualSequence;
    private Vector3 visualOrigin;
    private Vector3 visualDirection = Vector3.forward;
    private Quaternion visualRotation = Quaternion.identity;
    private float visualRange;
    private float visualWidth;
    private float visualPayloadDuration;
    private ulong visualCasterClientId = ulong.MaxValue;
    private ulong setupCasterNetworkObjectId = ulong.MaxValue;
    private bool hasCapturedBeamPose;
    private Vector3 capturedBeamOrigin;
    private Vector3 capturedBeamDirection = Vector3.forward;

    private void Awake()
    {
        ConfigureVisualRaycastMask();
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        ConfigureVisualRaycastMask();

        if (effectParticles != null)
            effectParticles.Play();

        netCaster.OnValueChanged += OnCasterAssigned;

        if (netCaster.Value.TryGet(out NetworkObject casterNO))
            SetupCaster(casterNO);
    }

    public override void OnNetworkDespawn()
    {
        netCaster.OnValueChanged -= OnCasterAssigned;
        base.OnNetworkDespawn();
    }

    public void StartUltimateEffect(GameObject casterObject, float damage, float range, float width, float delayBeforeBeam)
    {
        StartUltimateEffect(casterObject, damage, range, width, delayBeforeBeam, Vector3.zero, Vector3.zero);
    }

    public void StartUltimateEffect(
        GameObject casterObject,
        float damage,
        float range,
        float width,
        float delayBeforeBeam,
        Vector3 capturedOrigin,
        Vector3 capturedDirection)
    {
        if (!IsServer)
            return;

        caster = casterObject;
        SetCapturedBeamPose(capturedOrigin, capturedDirection);
        netDamage.Value = damage;
        netRange.Value = range;
        netWidth.Value = width;

        if (casterObject != null && casterObject.TryGetComponent(out NetworkObject casterNetworkObject))
        {
            netCaster.Value = new NetworkObjectReference(casterNetworkObject);
            SetupCaster(casterNetworkObject);
        }

        uint sequence = ++visualSequence;
        ulong casterClientId = ResolveCasterClientId(casterObject);

        StartCoroutine(ServerFireBeamAfterDelay(sequence, damage, range, width, delayBeforeBeam, casterClientId));
        StartCoroutine(ServerDespawnCoroutine(delayBeforeBeam));
    }

    public void StartOfflineUltimateEffect(GameObject casterObject, float damage, float range, float width, float delayBeforeBeam)
    {
        StartOfflineUltimateEffect(casterObject, damage, range, width, delayBeforeBeam, Vector3.zero, Vector3.zero);
    }

    public void StartOfflineUltimateEffect(
        GameObject casterObject,
        float damage,
        float range,
        float width,
        float delayBeforeBeam,
        Vector3 capturedOrigin,
        Vector3 capturedDirection)
    {
        caster = casterObject;
        SetCapturedBeamPose(capturedOrigin, capturedDirection);
        ConfigureVisualRaycastMask();
        StartCoroutine(OfflineFireBeamAfterDelay(damage, range, width, delayBeforeBeam, ResolveCasterClientId(casterObject)));
    }

    private void OnCasterAssigned(NetworkObjectReference oldVal, NetworkObjectReference newVal)
    {
        if (newVal.TryGet(out NetworkObject casterNO))
            SetupCaster(casterNO);
    }

    private void SetupCaster(NetworkObject casterNO)
    {
        if (casterNO == null)
            return;

        bool shouldTriggerAnimation = setupCasterNetworkObjectId != casterNO.NetworkObjectId;
        setupCasterNetworkObjectId = casterNO.NetworkObjectId;

        caster = casterNO.gameObject;
        universalAnimator = caster.GetComponentInChildren<UniversalCharacterAnimator>();

        AnimationEventProxy proxy = caster.GetComponentInChildren<AnimationEventProxy>();
        if (proxy != null)
            proxy.magiaAtualDaCacadora = this;

        if (IsServer && shouldTriggerAnimation && universalAnimator != null)
        {
            universalAnimator.TriggerAction(CharacterActionID.CacadoraUltimate);
        }
    }

    private IEnumerator ServerFireBeamAfterDelay(
        uint sequence,
        float damage,
        float range,
        float width,
        float delayBeforeBeam,
        ulong casterClientId)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delayBeforeBeam));

        ResolveBeamPose(out Vector3 origin, out Vector3 direction, out Quaternion rotation);
        CacheBeamVisualPayload(origin, direction, rotation, range, width, visualDuration, casterClientId);
        TryApplyBeamDamageOnce(origin, direction, damage, range, width);

        ForceBeamVisualClientRpc(sequence, origin, direction, rotation, range, width, visualDuration, casterClientId);
    }

    private IEnumerator OfflineFireBeamAfterDelay(
        float damage,
        float range,
        float width,
        float delayBeforeBeam,
        ulong casterClientId)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delayBeforeBeam));

        ResolveBeamPose(out Vector3 origin, out Vector3 direction, out Quaternion rotation);
        CacheBeamVisualPayload(origin, direction, rotation, range, width, visualDuration, casterClientId);
        ApplyBeamDamage(origin, direction, damage, range, width);

        if (beamVisualPrefab != null)
            StartCoroutine(ShowBeamVisual(origin, direction, rotation, range, width, visualDuration));

        Destroy(gameObject, visualDuration + 3.0f);
    }

    private IEnumerator ServerDespawnCoroutine(float delayBeforeBeam)
    {
        yield return new WaitForSeconds(Mathf.Max(0f, delayBeforeBeam) + visualDuration + 3.0f);

        if (NetworkObject != null && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }

    public void AnimEvent_FireBeam()
    {
        TryShowBeamVisualLocal();
    }

    [ClientRpc]
    private void ForceBeamVisualClientRpc(
        uint sequence,
        Vector3 origin,
        Vector3 direction,
        Quaternion rotation,
        float range,
        float width,
        float duration,
        ulong casterClientId)
    {
        TryShowBeamVisualLocal(sequence, origin, direction, rotation, range, width, duration, casterClientId);
    }

    private void TryApplyBeamDamageOnce(Vector3 origin, Vector3 direction, float damage, float range, float width)
    {
        if (!IsServer || hasAppliedBeamDamage)
            return;

        hasAppliedBeamDamage = true;
        ApplyBeamDamage(origin, direction, damage, range, width);
    }

    private void TryShowBeamVisualLocal()
    {
        if (!IsClient || beamVisualPrefab == null || hasShownBeamVisual || !hasBeamVisualPayload)
            return;

        hasShownBeamVisual = true;

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.LocalClientId == visualCasterClientId)
            JuiceEvents.OnCameraShake?.Invoke(visualDirection, ultimateShake.amplitude, ultimateShake.frequency, ultimateShake.duration);

        StartCoroutine(ShowBeamVisual(
            visualOrigin,
            visualDirection,
            visualRotation,
            visualRange,
            visualWidth,
            visualPayloadDuration));
    }

    private void TryShowBeamVisualLocal(
        uint sequence,
        Vector3 origin,
        Vector3 direction,
        Quaternion rotation,
        float range,
        float width,
        float duration,
        ulong casterClientId)
    {
        if (!IsClient || beamVisualPrefab == null || hasShownBeamVisual || lastShownVisualSequence == sequence)
            return;

        lastShownVisualSequence = sequence;
        CacheBeamVisualPayload(origin, direction, rotation, range, width, duration, casterClientId);
        TryShowBeamVisualLocal();
    }

    private IEnumerator ShowBeamVisual(
        Vector3 startPoint,
        Vector3 direction,
        Quaternion rotation,
        float range,
        float width,
        float duration)
    {
        ConfigureVisualRaycastMask();

        if (direction.sqrMagnitude <= 0.001f)
            direction = transform.forward;

        direction.Normalize();
        float beamDistance = Mathf.Max(0.1f, range);

        if (Physics.Raycast(startPoint, direction, out RaycastHit groundHit, beamDistance, visualRaycastMask))
            beamDistance = groundHit.distance;

        GameObject visual = Instantiate(beamVisualPrefab, startPoint, rotation);

        LineRenderer line = visual.GetComponent<LineRenderer>();
        if (line != null)
        {
            visual.transform.SetParent(transform);
            line.SetPosition(0, Vector3.zero);
            line.SetPosition(1, Vector3.forward * beamDistance);
            line.startWidth = width;
            line.endWidth = width;

            float elapsedTime = 0f;
            Material lineMaterial = line.material;
            Color originalColor = Color.white;
            if (lineMaterial != null && lineMaterial.HasColor("_Color"))
                originalColor = lineMaterial.color;

            while (elapsedTime < duration)
            {
                elapsedTime += Time.deltaTime;
                float progress = elapsedTime / duration;
                if (lineMaterial != null)
                {
                    originalColor.a = 1f - progress;
                    lineMaterial.color = originalColor;
                }
                yield return null;
            }

            if (visual != null) Destroy(visual);
            yield break;
        }

        float speed = beamDistance / Mathf.Max(duration, 0.1f);
        FlyForward flyForward = visual.GetComponent<FlyForward>();
        if (flyForward != null)
        {
            flyForward.speed = speed;
            flyForward.lifetime = duration;
            Destroy(visual, duration + 0.1f);
            yield break;
        }

        ArrowMover mover = visual.GetComponent<ArrowMover>();
        if (mover == null)
            mover = visual.AddComponent<ArrowMover>();

        mover.flyDirection = direction;
        mover.flySpeed = speed;
        mover.lifetime = duration;
    }

    private void CacheBeamVisualPayload(
        Vector3 origin,
        Vector3 direction,
        Quaternion rotation,
        float range,
        float width,
        float duration,
        ulong casterClientId)
    {
        if (direction.sqrMagnitude <= 0.001f)
            direction = transform.forward;

        visualOrigin = origin;
        visualDirection = direction.normalized;
        visualRotation = rotation;
        visualRange = range > 0.1f ? range : 100f;
        visualWidth = width > 0.1f ? width : 3f;
        visualPayloadDuration = Mathf.Max(0.1f, duration);
        visualCasterClientId = casterClientId;
        hasBeamVisualPayload = true;
    }

    private void ResolveBeamPose(out Vector3 origin, out Vector3 direction, out Quaternion rotation)
    {
        GameObject casterObject = caster;

        origin = hasCapturedBeamPose ? capturedBeamOrigin : transform.position;
        direction = hasCapturedBeamPose ? capturedBeamDirection : transform.forward;

        if (casterObject != null)
        {
            Transform firePoint = casterObject.transform;
            PlayerShooting shooting = casterObject.GetComponent<PlayerShooting>();
            if (shooting != null && shooting.firePoint != null)
                firePoint = shooting.firePoint;

            origin = firePoint.position;

            if (!hasCapturedBeamPose)
                direction = AbilityAimUtility.ResolveAimDirection3D(casterObject);
        }

        if (direction.sqrMagnitude <= 0.001f)
            direction = transform.forward.sqrMagnitude > 0.001f ? transform.forward : Vector3.forward;

        direction.Normalize();
        rotation = Quaternion.LookRotation(direction, Vector3.up);
        transform.SetPositionAndRotation(origin, rotation);
        Physics.SyncTransforms();
    }

    private void SetCapturedBeamPose(Vector3 origin, Vector3 direction)
    {
        if (direction.sqrMagnitude <= 0.001f)
        {
            hasCapturedBeamPose = false;
            return;
        }

        capturedBeamOrigin = origin;
        capturedBeamDirection = direction.normalized;
        hasCapturedBeamPose = true;
    }

    private ulong ResolveCasterClientId(GameObject casterObject)
    {
        if (casterObject != null && casterObject.TryGetComponent(out NetworkObject casterNetworkObject))
            return casterNetworkObject.OwnerClientId;

        return ulong.MaxValue;
    }

    private void ApplyBeamDamage(Vector3 origin, Vector3 direction, float damage, float range, float width)
    {
        LayerMask enemyLayer = LayerMask.GetMask("Enemy");
        RaycastHit[] inimigosAcertados = Physics.SphereCastAll(origin, width, direction, range, enemyLayer);
        HashSet<EnemyHealthSystem> damagedEnemies = new HashSet<EnemyHealthSystem>();

        foreach (RaycastHit hit in inimigosAcertados)
        {
            EnemyHealthSystem vidaInimigo = hit.collider.GetComponentInParent<EnemyHealthSystem>();
            if (vidaInimigo != null && damagedEnemies.Add(vidaInimigo))
                vidaInimigo.TakeDamage(damage, 0f, false);
        }
    }

    private void ConfigureVisualRaycastMask()
    {
        LayerMask enemyLayer = LayerMask.GetMask("Enemy");
        LayerMask playerLayer = LayerMask.GetMask("Player");
        visualRaycastMask = ~(enemyLayer | playerLayer);
    }
}

public class ArrowMover : MonoBehaviour
{
    [HideInInspector] public Vector3 flyDirection;
    [HideInInspector] public float flySpeed;
    [HideInInspector] public float lifetime = 2f;

    private float timer;

    private void Update()
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
