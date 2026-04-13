using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── HealingAuraBehavior ──────────────────────────────────
/// Heals nearby allied towers over time while the player is alive.
///
///  ▸ Server-only: heal tick runs exclusively on IsServer
///  ▸ Tick-based: heals every 1 second, not every frame
///  ▸ Cleanup via OnNetworkDespawn ensures no lingering heal on disconnect
/// ─────────────────────────────────────────────────────────
/// </summary>
public class HealingAuraBehavior : TowerBehavior
{
    public float auraRadius = 10f;
    public float healPerTick = 5f;

    private float healTimer;

    private void Update()
    {
        if (!IsServer) return;

        healTimer += Time.deltaTime;
        if (healTimer >= 1f)
        {
            HealTowersInRange();
            healTimer = 0f;
        }
    }

    private void HealTowersInRange()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, auraRadius);
        foreach (var col in colliders)
        {
            TowerController tower = col.GetComponent<TowerController>();
            if (tower != null)
            {
                tower.Heal(healPerTick);
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, auraRadius);
    }
}
