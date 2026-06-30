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
        Collider[] colliders = Physics.OverlapSphere(transform.position, towerController.CurrentRange);
        System.Collections.Generic.List<EnemyHealthSystem> validTargets = new System.Collections.Generic.List<EnemyHealthSystem>();

        foreach (var col in colliders)
        {
            EnemyHealthSystem ehs = col.GetComponent<EnemyHealthSystem>();
            if (ehs != null && !ehs.isDead && ehs != target) validTargets.Add(ehs);
        }

        for (int i = 0; i < freeArrows && validTargets.Count > 0; i++)
        {
            EnemyHealthSystem randomTarget = validTargets[Random.Range(0, validTargets.Count)];
            towerController.FireExtraProjectileAt(randomTarget, 1f);
        }
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnEnemyKilled -= HandleEnemyKilled;
        }
    }
}
