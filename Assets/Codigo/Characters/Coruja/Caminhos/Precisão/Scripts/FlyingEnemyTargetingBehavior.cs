using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── FlyingEnemyTargetingBehavior ─────────────────────────
/// Enables the tower to target airborne enemies.
///
///  ▸ Server-only: TargetsFlyingEnemies flag read server-side during targeting
///  ▸ OnDestroy resets the flag so the tower stops targeting fliers if behavior is removed
///  ▸ towerOwner cached separately to survive towerController base-class null check in OnDestroy
/// ─────────────────────────────────────────────────────────
/// </summary>
public class FlyingEnemyTargetingBehavior : TowerBehavior
{
    private TowerController towerOwner;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        this.towerOwner = owner;
        if (owner != null)
        {
            owner.TargetsFlyingEnemies = true;
        }
    }

    private void OnDestroy()
    {
        if (IsServer && towerOwner != null)
        {
            towerOwner.TargetsFlyingEnemies = false;
        }
    }
}
