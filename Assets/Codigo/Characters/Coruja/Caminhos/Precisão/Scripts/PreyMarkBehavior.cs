using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── PreyMarkBehavior ─────────────────────────────────────
/// Marks enemies damaged by the tower to receive bonus damage.
///
///  ▸ Server-only: ApplyMarkedStatus mutates EnemyHealthSystem state
///  ▸ Mark multiplier passed as (1 + bonus) to match EnemyHealthSystem's expected format
///  ▸ Cleanup in OnDestroy prevents dangling event references
/// ─────────────────────────────────────────────────────────
/// </summary>
public class PreyMarkBehavior : TowerBehavior
{
    public float damageBonusToMarked = 0.3f;
    public float markDuration = 10f;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
        {
            towerController.OnTargetDamaged += HandleTowerAttack;
        }
    }

    private void HandleTowerAttack(EnemyHealthSystem target)
    {
        if (target != null)
        {
            target.ApplyMarkedStatus(1.0f + damageBonusToMarked, markDuration);
        }
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnTargetDamaged -= HandleTowerAttack;
        }
    }
}
