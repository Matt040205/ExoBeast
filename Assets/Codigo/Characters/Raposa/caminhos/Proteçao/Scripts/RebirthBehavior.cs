using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── RebirthBehavior ──────────────────────────────────────
/// Revives a nearby destroyed tower after a cooldown.
///
///  ▸ Server-only: Revive modifies tower health state on server
///  ▸ One revival per cooldown cycle; loop exits after first target found
///  ▸ Cooldown prevents re-triggering before enough time has passed
/// ─────────────────────────────────────────────────────────
/// </summary>
public class RebirthBehavior : TowerBehavior
{
    [Header("Configuracao do Renascimento")]
    public float reviveHealthPercentage = 0.20f;
    public float cooldown = 90f;
    public float scanRadius = 10f;

    private float cooldownTimer;

    private void Update()
    {
        if (!IsServer) return;

        if (cooldownTimer > 0)
        {
            cooldownTimer -= Time.deltaTime;
            return;
        }

        FindAndReviveTower();
    }

    private void FindAndReviveTower()
    {
        Collider[] colliders = Physics.OverlapSphere(transform.position, scanRadius);
        foreach (var col in colliders)
        {
            TowerController otherTower = col.GetComponent<TowerController>();
            if (otherTower != null && otherTower.IsDestroyed && otherTower != this.towerController)
            {
                otherTower.Revive(reviveHealthPercentage);
                cooldownTimer = cooldown;
                return;
            }
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(transform.position, scanRadius);
    }
}
