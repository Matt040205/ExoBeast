using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── PaintParalyzeBehavior ──────────────────────────────────
/// Paralyzes the enemy after accumulating 5 consecutive paint hits.
/// ───────────────────────────────────────────────────────────
/// </summary>
public class PaintParalyzeBehavior : TowerBehavior
{
    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
        {
            towerController.OnTargetDamaged += HandleTargetHit;
        }
    }

    private void HandleTargetHit(EnemyHealthSystem target)
    {
        EnemyController enemy = target.GetComponent<EnemyController>();
        if (enemy != null)
        {
            enemy.AddPaintStack();
        }
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnTargetDamaged -= HandleTargetHit;
        }
    }
}
