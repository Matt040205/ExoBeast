using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── ReloadSpeedBehavior ──────────────────────────────────
/// Temporarily boosts the tower's reload speed after a critical hit.
///
///  ▸ Server-only: reload speed is a server-side tower stat
///  ▸ Buff duration tracked server-side; expired via coroutine (pending full impl)
///  ▸ Cleanup in OnDestroy prevents dangling event references
/// ─────────────────────────────────────────────────────────
/// </summary>
public class ReloadSpeedBehavior : TowerBehavior
{
    public float reloadSpeedBonus = 0.3f;
    public float bonusDuration = 4f;

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
        // Apply timed reload-speed buff via TowerController when API is available
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnCriticalHit -= HandleCriticalHit;
        }
    }
}
