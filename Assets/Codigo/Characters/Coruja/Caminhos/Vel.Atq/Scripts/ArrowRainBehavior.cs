using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── ArrowRainBehavior ────────────────────────────────────
/// Fires bonus arrows when the tower kills an enemy.
///
///  ▸ Server-only: extra projectile logic runs on server authority
///  ▸ freeArrows count fed into TowerController fire logic (pending full impl)
///  ▸ Cleanup in OnDestroy prevents dangling event references
/// ─────────────────────────────────────────────────────────
/// </summary>
public class ArrowRainBehavior : TowerBehavior
{
    public int freeArrows = 3;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
        {
            towerController.OnEnemyKilled += HandleEnemyKilled;
        }
    }

    private void HandleEnemyKilled(EnemyHealthSystem target)
    {
        // Fire freeArrows extra shots via TowerController when API is available
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnEnemyKilled -= HandleEnemyKilled;
        }
    }
}
