using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// ── LegacyAuraBehavior ───────────────────────────────────
/// Grants a damage and attack-speed bonus to allied towers within a radius.
///
///  ▸ Server-only: all buff application guarded by IsServer
///  ▸ Tick-based: aura re-evaluated every 0.5 s to reduce physics overhead
///  ▸ Cleanup on OnNetworkDespawn removes bonuses if player disconnects
/// ─────────────────────────────────────────────────────────
/// </summary>
public class LegacyAuraBehavior : TowerBehavior
{
    [Header("Configuracao do Legado")]
    public float damageBonus = 0.25f;
    public float attackSpeedBonus = 0.15f;
    public float auraRadius = 8f;

    private List<TowerController> affectedTowers = new List<TowerController>();
    private float timer;

    private void Update()
    {
        if (!IsServer) return;

        timer += Time.deltaTime;
        if (timer >= 0.5f)
        {
            UpdateAura();
            timer = 0f;
        }
    }

    private void UpdateAura()
    {
        var towersInRange = FindTowersInRange();

        foreach (var tower in affectedTowers.ToList())
        {
            if (!towersInRange.Contains(tower))
            {
                if (tower != null)
                {
                    tower.AddDamageBonus(-damageBonus);
                    tower.AddAttackSpeedBonus(-attackSpeedBonus);
                }
                affectedTowers.Remove(tower);
            }
        }

        foreach (var tower in towersInRange)
        {
            if (!affectedTowers.Contains(tower))
            {
                tower.AddDamageBonus(damageBonus);
                tower.AddAttackSpeedBonus(attackSpeedBonus);
                affectedTowers.Add(tower);
            }
        }
    }

    private List<TowerController> FindTowersInRange()
    {
        return Physics.OverlapSphere(transform.position, auraRadius)
            .Select(col => col.GetComponent<TowerController>())
            .Where(tower => tower != null && tower != this.towerController)
            .ToList();
    }

    private void OnDestroy()
    {
        foreach (var tower in affectedTowers)
        {
            if (tower != null)
            {
                tower.AddDamageBonus(-damageBonus);
                tower.AddAttackSpeedBonus(-attackSpeedBonus);
            }
        }
        affectedTowers.Clear();
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, auraRadius);
    }
}
