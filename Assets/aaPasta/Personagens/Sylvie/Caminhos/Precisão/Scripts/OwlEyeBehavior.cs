using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── OwlEyeBehavior ───────────────────────────────────────
/// Reveals enemies hit by critical shots for a duration.
///
///  ▸ Server-only: initialized in IsServer guard so only server subscribes
///  ▸ Reveal visual pushed to clients via ClientRpc when EnemyHealthSystem supports it
///  ▸ Cleanup in OnDestroy prevents dangling event references
/// ─────────────────────────────────────────────────────────
/// </summary>
public class OwlEyeBehavior : TowerBehavior
{
    public float revealDuration = 5f;

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
            target.ApplyReveal(revealDuration); 
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
