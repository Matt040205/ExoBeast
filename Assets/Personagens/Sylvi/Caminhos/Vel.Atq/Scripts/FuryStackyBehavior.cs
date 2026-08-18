using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── FuryStackyBehavior ───────────────────────────────────
/// Stacks an attack-rate bonus on the tower with each critical hit (up to maxStacks).
///
///  ▸ Server-only: attack-rate modifications are authoritative on server
///  ▸ currentStacks tracked server-side; no NetworkVariable needed for tower buffs
///  ▸ Cleanup in OnDestroy prevents dangling event references
/// ─────────────────────────────────────────────────────────
/// </summary>
public class FuryStackyBehavior : TowerBehavior
{
    public float bonusPerStack = 0.05f;
    public int maxStacks = 8;
    private int currentStacks;

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
        if (currentStacks < maxStacks)
        {
            currentStacks++;
            towerController.AddAttackSpeedBonus(bonusPerStack);
        }
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnCriticalHit -= HandleCriticalHit;
            for (int i = 0; i < currentStacks; i++)
            {
                towerController.AddAttackSpeedBonus(-bonusPerStack);
            }
            currentStacks = 0;
        }
    }
}
