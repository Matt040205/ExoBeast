using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;
using Unity.Netcode;
using Unity.Netcode.Components;

public enum WeaponType { Sword, Hammer }

[System.Serializable]
public class WeaponConfig
{
    [Header("Atributos Físicos")]
    public float attackRange = 2f;
    public float attackAngle = 90f;
    public float animationSpeed = 1f;

    [Header("Danos do Combo")]
    public float damageHit1 = 15f;
    public float damageHit2 = 15f;
    public float damageHit3 = 20f;
    public float damageHit4 = 30f;

    [Header("Sons FMOD")]
    [EventRef] public string sfxHit1;
    [EventRef] public string sfxHit2;
    [EventRef] public string sfxHit3;
    [EventRef] public string sfxHit4;
}

/// <summary>
/// ── MeleeCombatSystem ──────────────────────────────────
/// Sistema de combate corpo-a-corpo com combo de 4 hits.
///
///  ▸ Owner: detecta input de ataque, dispara trigger via NetworkAnimator
///  ▸ AnimEvents (DetectHits): som toca em todos, OverlapSphere apenas no owner
///  ▸ RequestMeleeDamageServerRpc: dano com armor pen e crit validado no servidor
///  ▸ Suporta Sword e Hammer com stats independentes
/// ─────────────────────────────────────────────────────
/// </summary>
public class MeleeCombatSystem : NetworkBehaviour
{
    [Header("Configuração Geral")]
    public CharacterBase characterData;
    public Transform attackPoint;
    public LayerMask hitLayers;
    public WeaponType currentWeaponType = WeaponType.Sword;

    [Header("Status das Armas")]
    public WeaponConfig swordStats;
    public WeaponConfig hammerStats;

    [Header("Overrides de Sistema (Ultimate)")]
    public float? overrideAttackSpeed = null;
    public float? overrideAttackAngle = null;

    [Header("VFX de Golpe")]
    [Tooltip("Prefab do efeito slash (arco de corte) que aparece a cada espadada.")]
    [SerializeField] private GameObject slashVfxPrefab;

    [Header("Juice Configs")]
    [SerializeField] private CameraShakeConfig ultimateHitShake = new CameraShakeConfig(0.8f, 15f, 0.1f);

    private Animator anim;
    private NetworkAnimator networkAnimator; // <--- CACHE SEGURO ADICIONADO AQUI
    private WeaponConfig currentStats;
    private LocalPlayerInputBridge inputBridge;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Inicializar anim para TODOS (owner e remotos): AnimEvents de som precisam rodar em todos
        anim = GetComponentInChildren<Animator>();

        // Garantir velocidade de ataque correta desde o spawn (evita animacoes congeladas em 0)
        if (anim != null)
            anim.SetFloat("AttackSpeedMultiplier", 1f);

        if (!IsOwner)
        {
            // Script permanece ativo para AnimEvents de som (DetectHits dispara som em todos)
            // Apenas input e logica de deteccao de hit sao bloqueados via guard no Update/OnFire
            return;
        }

        networkAnimator = GetComponent<NetworkAnimator>();
        if (networkAnimator == null) networkAnimator = GetComponentInChildren<NetworkAnimator>();

        inputBridge = GetComponent<LocalPlayerInputBridge>();

        UpdateCurrentStats();
    }

    private bool hitProcessedForCurrentAttack = false;
    private bool isInAttackSequence = false;
    private PlayerMovement cachedMovement;

    /// <summary>
    /// BUG FIX (Bug 9 - 7 Maio 2026): forca o modelPivot a apontar para a camera ANTES de
    /// disparar o trigger de ataque. Sem isso, attackPoint (child do modelPivot) ficava na
    /// rotacao do spawn quando o jogador estava parado e nao mirando — Dragao so batia para
    /// o lado em que spawnou. Centralizado num helper para Update() e OnFire() compartilharem.
    /// </summary>
    private void FaceCameraBeforeAttack()
    {
        if (cachedMovement == null) cachedMovement = GetComponent<PlayerMovement>();
        if (cachedMovement != null) cachedMovement.FaceCameraImmediately();
    }

    private Vector3 ResolveAttackForward()
    {
        Vector3 forward = AbilityAimUtility.ResolveAimForward(gameObject);
        if (forward.sqrMagnitude <= 0.0001f && attackPoint != null)
            forward = attackPoint.forward;

        forward.y = 0f;
        if (forward.sqrMagnitude <= 0.0001f)
            forward = transform.forward;

        forward.y = 0f;
        return forward.sqrMagnitude > 0.0001f ? forward.normalized : Vector3.forward;
    }

    public void OnFire(InputAction.CallbackContext ctx)
    {
        if (!IsOwner || !this.enabled) return;

        // Bridge está ativo em multiplayer — o polling em Update() já cobre o input.
        // Sem este guard, um ataque dispararia duas vezes (callback + polling) no mesmo frame.
        if (inputBridge != null && inputBridge.isActiveAndEnabled) return;

        if (ctx.performed && !PauseControl.isPaused && !BuildManager.isBuildingMode)
        {
            FaceCameraBeforeAttack();

            // CORREÇÃO DA LINHA 79: Usando a referência segura para o NetworkAnimator
            if (networkAnimator != null) networkAnimator.SetTrigger("Attack");

            hitProcessedForCurrentAttack = false;
            isInAttackSequence = true;
            StartCoroutine(FallbackHitRoutine());
        }
    }

    private System.Collections.IEnumerator FallbackHitRoutine()
    {
        float targetSpeed = overrideAttackSpeed.HasValue ? overrideAttackSpeed.Value : currentStats.animationSpeed;
        float delay = 0.4f / (targetSpeed > 0.1f ? targetSpeed : 1f);
        
        yield return new WaitForSeconds(delay);

        if (!hitProcessedForCurrentAttack)
        {
            AnimEvent_Hit1(); // Força o hit pro caso da animação não existir ou falhar!
        }
    }

    void Update()
    {
        // Velocidade de ataque deve ser aplicada em todos os clientes para animar corretamente
        UpdateCurrentStats();

        if (anim != null)
        {
            float targetSpeed = overrideAttackSpeed.HasValue ? overrideAttackSpeed.Value : currentStats.animationSpeed;
            anim.SetFloat("AttackSpeedMultiplier", targetSpeed);
        }

        if (IsOwner)
        {
            if (inputBridge == null) inputBridge = GetComponent<LocalPlayerInputBridge>();
            if (inputBridge != null && inputBridge.isActiveAndEnabled)
            {
                if (inputBridge.ConsumeMeleeAttackPressed() &&
                    !PauseControl.isPaused && !BuildManager.isBuildingMode)
                {
                    FaceCameraBeforeAttack();

                    if (networkAnimator != null) networkAnimator.SetTrigger("Attack");
                    hitProcessedForCurrentAttack = false;
                    isInAttackSequence = true;
                    StartCoroutine(FallbackHitRoutine());
                }
            }
        }
    }

    void OnDisable()
    {
        if (anim != null) anim.SetFloat("AttackSpeedMultiplier", 1.0f);
    }

    private void UpdateCurrentStats()
    {
        currentStats = (currentWeaponType == WeaponType.Sword) ? swordStats : hammerStats;
    }

    private void DetectHits(float damageToApply, string fmodEvent)
    {
        if (!string.IsNullOrEmpty(fmodEvent))
            RuntimeManager.PlayOneShot(fmodEvent, transform.position);

        // VFX de slash: aparece APENAS com a espada (Sword) e durante um ataque real (não ao sacar)
        if (slashVfxPrefab != null && attackPoint != null && currentWeaponType == WeaponType.Sword && isInAttackSequence)
        {
            Quaternion slashRot = attackPoint.rotation * Quaternion.Euler(0f, 0f, Random.Range(-30f, 30f));
            GlobalVFXPool.GetVFX(slashVfxPrefab, attackPoint.position, slashRot, 1.5f);
        }

        if (!IsOwner) return;

        // Gatilho EXCLUSIVO para Ultimate Dança das Nove Caudas (Identificada pelos Overrides)
        if (overrideAttackSpeed.HasValue)
        {
            Vector3 randomDir = new Vector3(Random.Range(-1f, 1f), 0f, Random.Range(-1f, 1f)).normalized;
            JuiceEvents.OnCameraShake?.Invoke(randomDir, ultimateHitShake.amplitude, ultimateHitShake.frequency, ultimateHitShake.duration);
        }

        float currentAngle = overrideAttackAngle ?? currentStats.attackAngle;
        float currentRange = currentStats.attackRange;
        Vector3 attackOrigin = attackPoint != null ? attackPoint.position : transform.position;
        Vector3 attackForward = ResolveAttackForward();

        Collider[] hitTargets = Physics.OverlapSphere(attackOrigin, currentRange, hitLayers);

        foreach (Collider target in hitTargets)
        {
            Vector3 directionToTarget = target.transform.position - attackOrigin;
            directionToTarget.y = 0f;

            if (directionToTarget.sqrMagnitude <= 0.0001f)
                continue;

            float angleToTarget = Vector3.Angle(attackForward, directionToTarget.normalized);

            if (angleToTarget < currentAngle / 2)
            {
                if (target.TryGetComponent<NetworkObject>(out var netObj))
                {
                    float armorPen = (characterData != null) ? characterData.armorPenetration : 0f;
                    bool isCrit = (characterData != null) && (Random.value <= characterData.critChance);
                    float finalDamage = damageToApply;
                    if (isCrit && characterData != null) finalDamage *= characterData.critDamage;

                    // Passo 2: Acesso rápido e direto ao sistema de rede do alvo.
                    var networkedEnemy = netObj.GetComponent<ExoBeasts.Multiplayer.Sync.NetworkedEnemy>();
                    if (networkedEnemy != null && networkedEnemy.IsSpawned)
                    {
                        // Chamamos o RPC descentralizado direto do inimigo.
                        // O NGO irá encapsular nosso ID (1, 2, etc.) automaticamente na chegada ao servidor.
                        networkedEnemy.TakeDamageServerRpc(finalDamage, armorPen, isCrit);
                        if (JuiceManager.Instance != null) JuiceManager.Instance.HitStop(0.05f);
                    }
                }
            }
        }
    }

    public void AnimEvent_Hit1() 
    { 
        hitProcessedForCurrentAttack = true; 
        DetectHits(currentStats.damageHit1, currentStats.sfxHit1); 
    }
    public void AnimEvent_Hit2() 
    { 
        hitProcessedForCurrentAttack = true; 
        DetectHits(currentStats.damageHit2, currentStats.sfxHit2); 
    }
    public void AnimEvent_Hit3() 
    { 
        hitProcessedForCurrentAttack = true; 
        DetectHits(currentStats.damageHit3, currentStats.sfxHit3); 
    }
    public void AnimEvent_Hit4() 
    { 
        hitProcessedForCurrentAttack = true; 
        DetectHits(currentStats.damageHit4, currentStats.sfxHit4); 
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        WeaponConfig statsToDraw = (currentWeaponType == WeaponType.Sword) ? swordStats : hammerStats;
        if (statsToDraw == null) return;

        float currentAngle = (overrideAttackAngle != null) ? overrideAttackAngle.Value : statsToDraw.attackAngle;
        float currentRange = statsToDraw.attackRange;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, currentRange);
        Vector3 attackForward = Application.isPlaying ? ResolveAttackForward() : AbilityAimUtility.ResolveFlatForward(attackPoint);
        Vector3 leftBound = Quaternion.Euler(0, -currentAngle / 2, 0) * attackForward * currentRange;
        Vector3 rightBound = Quaternion.Euler(0, currentAngle / 2, 0) * attackForward * currentRange;
        Gizmos.DrawLine(attackPoint.position, attackPoint.position + leftBound);
        Gizmos.DrawLine(attackPoint.position, attackPoint.position + rightBound);
    }
}
