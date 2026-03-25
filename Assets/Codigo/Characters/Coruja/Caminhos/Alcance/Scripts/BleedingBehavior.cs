using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── BleedingBehavior ─────────────────────────────────────
/// Applies a bleed-over-time effect to enemies hit by critical shots.
///
///  ▸ Server-only: initialized in IsServer guard so only server subscribes
///  ▸ ApplyBleed requires implementation in EnemyHealthSystem (pending)
///  ▸ Cleanup in OnDestroy prevents dangling event references
/// ─────────────────────────────────────────────────────────
/// </summary>
public class BleedingBehavior : TowerBehavior
{
    public float bleedDuration = 3f;
    public float bleedDamagePerSecond = 5f;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
        {
            towerController.OnCriticalHit += HandleCriticalHit;
        }
    }

    private void HandleCriticalHit(EnemyHealthSystem target)
    {
        if (target != null)
        {
            // target.ApplyBleed(bleedDamagePerSecond, bleedDuration); — pending EnemyHealthSystem implementation
        }
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnCriticalHit -= HandleCriticalHit;
        }
    }
}
