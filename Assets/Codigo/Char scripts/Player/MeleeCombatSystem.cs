using UnityEngine;
using UnityEngine.InputSystem;
using FMODUnity;

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

public class MeleeCombatSystem : MonoBehaviour
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
    private WeaponConfig currentStats;

    void Start()
    {
        anim = GetComponentInChildren<Animator>();
        UpdateCurrentStats();
    }

    public void OnFire(InputAction.CallbackContext ctx)
    {
        if (ctx.performed && !PauseControl.isPaused && !BuildManager.isBuildingMode)
        {
            anim.SetTrigger("Attack");
        }
    }

    void Update()
    {
        UpdateCurrentStats();

        if (overrideAttackSpeed.HasValue)
            anim.speed = overrideAttackSpeed.Value;
        else
            anim.speed = currentStats.animationSpeed;
    }

    void OnDisable()
    {
        if (anim != null) anim.speed = 1.0f;
    }

    private void UpdateCurrentStats()
    {
        currentStats = (currentWeaponType == WeaponType.Sword) ? swordStats : hammerStats;
    }

    private void DetectHits(float damageToApply, string fmodEvent)
    {
        if (!string.IsNullOrEmpty(fmodEvent))
            RuntimeManager.PlayOneShot(fmodEvent, transform.position);

        float currentAngle = overrideAttackAngle ?? currentStats.attackAngle;
        float currentRange = currentStats.attackRange;

        Collider[] hitTargets = Physics.OverlapSphere(attackPoint.position, currentRange, hitLayers);

        foreach (Collider target in hitTargets)
        {
            Vector3 directionToTarget = (target.transform.position - attackPoint.position).normalized;
            float angleToTarget = Vector3.Angle(attackPoint.forward, directionToTarget);

            if (angleToTarget < currentAngle / 2)
            {
                EnemyHealthSystem enHealth = target.GetComponent<EnemyHealthSystem>();
                if (enHealth != null) enHealth.TakeDamage(damageToApply);
            }
        }
    }

    public void AnimEvent_Hit1() => DetectHits(currentStats.damageHit1, currentStats.sfxHit1);
    public void AnimEvent_Hit2() => DetectHits(currentStats.damageHit2, currentStats.sfxHit2);
    public void AnimEvent_Hit3() => DetectHits(currentStats.damageHit3, currentStats.sfxHit3);
    public void AnimEvent_Hit4() => DetectHits(currentStats.damageHit4, currentStats.sfxHit4);

    void OnDrawGizmosSelected()
    {
        if (attackPoint == null) return;
        WeaponConfig statsToDraw = (currentWeaponType == WeaponType.Sword) ? swordStats : hammerStats;
        if (statsToDraw == null) return;

        float currentAngle = overrideAttackAngle ?? statsToDraw.attackAngle;
        float currentRange = statsToDraw.attackRange;

        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(attackPoint.position, currentRange);
        Vector3 leftBound = Quaternion.Euler(0, -currentAngle / 2, 0) * attackPoint.forward * currentRange;
        Vector3 rightBound = Quaternion.Euler(0, currentAngle / 2, 0) * attackPoint.forward * currentRange;
        Gizmos.DrawLine(attackPoint.position, attackPoint.position + leftBound);
        Gizmos.DrawLine(attackPoint.position, attackPoint.position + rightBound);
    }
}