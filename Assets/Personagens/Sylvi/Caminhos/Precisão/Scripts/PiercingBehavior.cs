using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── PiercingBehavior ─────────────────────────────────────
/// Makes tower arrows pierce through a configurable number of enemies.
///
///  ▸ Server-only: projectile behavior modification runs on server authority
///  ▸ enemiesToPierce feeds into TowerController projectile logic (pending full impl)
///  ▸ No events to unsubscribe — behavior applied once at Initialize time
/// ─────────────────────────────────────────────────────────
/// </summary>
public class PiercingBehavior : TowerBehavior
{
    public int enemiesToPierce = 1;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        // enemiesToPierce read by TowerController when building the projectile
    }
}
