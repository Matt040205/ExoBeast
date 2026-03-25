using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── FuryStackBehavior ────────────────────────────────────
/// Increases tower damage with each consecutive hit on the same target.
///
///  ▸ Server-only: both event handlers guarded by IsServer in Initialize
///  ▸ Bonus applies from the second consecutive hit onward (effectiveStacks - 1)
///  ▸ Stacks reset to 1 when target changes (not 0, to count the current hit)
/// ─────────────────────────────────────────────────────────
/// </summary>
public class FuryStackBehavior : TowerBehavior
{
    [Header("Configuracoes da Furia")]
    [Tooltip("Damage bonus per stack. 0.08 = 8%.")]
    public float damageBonusPerStack = 0.08f;
    public int maxStacks = 6;

    private int currentStacks;
    private EnemyHealthSystem lastTarget;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
        {
            towerController.OnTargetDamaged += HandleTargetHit;
            towerController.OnCalculateDamage += ApplyFuryDamage;
        }
    }

    private void HandleTargetHit(EnemyHealthSystem newTarget)
    {
        if (newTarget != null && newTarget == lastTarget)
        {
            if (currentStacks < maxStacks)
            {
                currentStacks++;
            }
        }
        else
        {
            // Target changed — start a fresh streak counting this hit as 1
            currentStacks = 1;
        }

        lastTarget = newTarget;
    }

    private float ApplyFuryDamage(EnemyHealthSystem target, float currentDamage)
    {
        // Bonus only starts from the second consecutive hit
        int effectiveStacks = currentStacks - 1;
        if (effectiveStacks > 0)
        {
            return currentDamage + currentDamage * (effectiveStacks * damageBonusPerStack);
        }
        return currentDamage;
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnTargetDamaged -= HandleTargetHit;
            towerController.OnCalculateDamage -= ApplyFuryDamage;
        }
    }
}
