using System.Collections.Generic;
using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── MultiShotBehavior ────────────────────────────────────
/// Fires extra projectiles at nearby enemies when the tower attacks.
///
///  ▸ Server-only: extra-shot logic runs exclusively on server authority
///  ▸ Picks random valid targets within tower range, excluding main target
///  ▸ FireProjectileAt implementation left to TowerController (TODO)
/// ─────────────────────────────────────────────────────────
/// </summary>
public class MultiShotBehavior : TowerBehavior
{
    [Header("Configuracao do Multi-Tiro")]
    public int extraProjectiles = 3;
    [Tooltip("Damage of extra projectiles relative to base damage. 0.5 = 50%.")]
    public float damageMultiplier = 0.5f;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
        {
            towerController.OnTargetDamaged += FireExtraProjectiles;
        }
    }

    private void FireExtraProjectiles(EnemyHealthSystem mainTarget)
    {
        Collider[] colliders = Physics.OverlapSphere(towerController.transform.position, towerController.CurrentRange);

        List<EnemyHealthSystem> validTargets = new List<EnemyHealthSystem>();
        foreach (var col in colliders)
        {
            EnemyHealthSystem target = col.GetComponent<EnemyHealthSystem>();
            if (target != null && target != mainTarget && !target.isDead)
            {
                validTargets.Add(target);
            }
        }

        for (int i = 0; i < extraProjectiles && validTargets.Count > 0; i++)
        {
            EnemyHealthSystem randomTarget = validTargets[Random.Range(0, validTargets.Count)];
            towerController.FireExtraProjectileAt(randomTarget, damageMultiplier);
        }
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnTargetDamaged -= FireExtraProjectiles;
        }
    }
}
