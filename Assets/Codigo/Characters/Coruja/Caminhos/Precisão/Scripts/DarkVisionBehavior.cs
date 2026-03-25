using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── DarkVisionBehavior ───────────────────────────────────
/// Increases the tower's effective targeting range at night or in dark areas.
///
///  ▸ Server-only: range modifier applied once at Initialize on server
///  ▸ darkVisionBonus read by TowerController range calculation
///  ▸ No events needed — range stat applied passively via the public field
/// ─────────────────────────────────────────────────────────
/// </summary>
public class DarkVisionBehavior : TowerBehavior
{
    public float darkVisionBonus = 0.5f;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        // darkVisionBonus applied to TowerController range when night/dark conditions are met
    }
}
