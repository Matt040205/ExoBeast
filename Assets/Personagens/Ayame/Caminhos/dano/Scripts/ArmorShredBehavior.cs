using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── ArmorShredBehavior ───────────────────────────────────
/// Applies a stacking armor-reduction debuff on tower attacks.
///
///  ▸ Server-only: ApplyArmorShred mutates EnemyHealthSystem state
///  ▸ Uses IsServer (inherited from TowerBehavior) instead of NetworkManager.Singleton.IsServer
///  ▸ Cleanup in OnDestroy prevents dangling event references
/// ─────────────────────────────────────────────────────────
/// </summary>
public class ArmorShredBehavior : TowerBehavior
{
    public float armorReductionPercentage = 0.05f;
    public int maxStacks = 3;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (towerController != null) towerController.OnTargetDamaged += HandleTowerAttack;
    }

    private void HandleTowerAttack(EnemyHealthSystem target)
    {
        if (!IsServer) return;
        if (target != null)
        {
            target.ApplyArmorShred(armorReductionPercentage, maxStacks);
        }
    }

    private void OnDestroy()
    {
        if (towerController != null) towerController.OnTargetDamaged -= HandleTowerAttack;
    }
}
