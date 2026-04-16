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

    private Animator anim;
    private NetworkAnimator networkAnimator; // <--- CACHE SEGURO ADICIONADO AQUI
    private WeaponConfig currentStats;

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

        UpdateCurrentStats();
    }

    private bool hitProcessedForCurrentAttack = false;

    public void OnFire(InputAction.CallbackContext ctx)
    {
        if (!IsOwner || !this.enabled) return;

        if (ctx.performed && !PauseControl.isPaused && !BuildManager.isBuildingMode)
        {
            // CORREÇÃO DA LINHA 79: Usando a referência segura para o NetworkAnimator
            if (networkAnimator != null) networkAnimator.SetTrigger("Attack");
            
            hitProcessedForCurrentAttack = false;
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

        if (!IsOwner) return;

        float currentAngle = overrideAttackAngle ?? currentStats.attackAngle;
        float currentRange = currentStats.attackRange;

        Collider[] hitTargets = Physics.OverlapSphere(attackPoint.position, currentRange, hitLayers);

        foreach (Collider target in hitTargets)
        {
            Vector3 directionToTarget = (target.transform.position - attackPoint.position).normalized;
            float angleToTarget = Vector3.Angle(attackPoint.forward, directionToTarget);

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
        Vector3 leftBound = Quaternion.Euler(0, -currentAngle / 2, 0) * attackPoint.forward * currentRange;
        Vector3 rightBound = Quaternion.Euler(0, currentAngle / 2, 0) * attackPoint.forward * currentRange;
        Gizmos.DrawLine(attackPoint.position, attackPoint.position + leftBound);
        Gizmos.DrawLine(attackPoint.position, attackPoint.position + rightBound);
    }
}