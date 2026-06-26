using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── BonusDamageToShreddedBehavior ────────────────────────
/// Increases tower damage against enemies with shredded armor.
///
///  ▸ Server-only: damage calculation events only fire on server
///  ▸ Hooks into TowerController.OnCalculateDamage for multiplicative bonus
///  ▸ Cleanup in OnDestroy prevents dangling event references
/// ─────────────────────────────────────────────────────────
/// </summary>
public class BonusDamageToShreddedBehavior : TowerBehavior
{
    [Header("Configuracao do Bonus")]
    [Tooltip("Multiplicative damage bonus against armor-shredded targets. 0.2 = 20%.")]
    public float damageBonus = 0.2f;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
        {
            towerController.OnCalculateDamage += ApplyDamageBonus;
        }
    }

    private float ApplyDamageBonus(EnemyHealthSystem target, float currentDamage)
    {
        if (target != null && target.IsArmorShredded)
        {
            return currentDamage * (1 + damageBonus);
        }
        return currentDamage;
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnCalculateDamage -= ApplyDamageBonus;
        }
    }
}
