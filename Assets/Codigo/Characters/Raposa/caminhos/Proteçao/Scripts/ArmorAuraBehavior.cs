using UnityEngine;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// ── ArmorAuraBehavior ────────────────────────────────────
/// Grants an armor bonus to allied towers within the aura radius.
///
///  ▸ Server-only: all buff/debuff calls are guarded by IsServer in Update
///  ▸ Tracks affected towers to correctly remove bonuses when they leave range
///  ▸ OnNetworkDespawn strips all bonuses if the player disconnects mid-session
/// ─────────────────────────────────────────────────────────
/// </summary>
public class ArmorAuraBehavior : NetworkBehaviour
{
    public float auraRadius = 10f;
    public float armorBonus = 0.2f;

    private List<TowerController> affectedTowers = new List<TowerController>();

    private void Update()
    {
        if (!IsServer) return;

        UpdateAuraEffect();
    }

    private void UpdateAuraEffect()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, auraRadius);
        List<TowerController> towersInRange = new List<TowerController>();

        foreach (var collider in colliders)
        {
            TowerController tower = collider.GetComponent<TowerController>();
            if (tower != null)
            {
                towersInRange.Add(tower);
            }
        }

        for (int i = affectedTowers.Count - 1; i >= 0; i--)
        {
            var tower = affectedTowers[i];
            if (tower == null || !towersInRange.Contains(tower))
            {
                if (tower != null) tower.AddArmorBonus(-armorBonus);
                affectedTowers.RemoveAt(i);
            }
        }

        foreach (var tower in towersInRange)
        {
            if (!affectedTowers.Contains(tower))
            {
                tower.AddArmorBonus(armorBonus);
                affectedTowers.Add(tower);
            }
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            foreach (var tower in affectedTowers)
            {
                if (tower != null) tower.AddArmorBonus(-armorBonus);
            }
            affectedTowers.Clear();
        }
        base.OnNetworkDespawn();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.blue;
        Gizmos.DrawWireSphere(transform.position, auraRadius);
    }
}
