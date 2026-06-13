using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── ProjectileSpeedBehavior ──────────────────────────────
/// Boosts the travel speed of projectiles fired by this tower.
///
///  ▸ Server-only: projectile speed is set at spawn time on the server
///  ▸ projectileSpeedBonus read by TowerController when building each projectile
///  ▸ No events needed — stat applied passively via the public field
/// ─────────────────────────────────────────────────────────
/// </summary>
public class ProjectileSpeedBehavior : TowerBehavior
{
    public float projectileSpeedBonus = 0.2f;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
        {
            towerController.AddAttackSpeedBonus(projectileSpeedBonus);
        }
    }
}
