using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── SpiritualBarrierBehavior ─────────────────────────────
/// Absorbs a percentage of incoming damage from nearby allied towers.
///
///  ▸ Called by other towers during their TakeDamage flow (server-side)
///  ▸ This tower absorbs the shielded portion and takes it as self-damage
///  ▸ Returns the remaining damage to be applied to the original target
/// ─────────────────────────────────────────────────────────
/// </summary>
public class SpiritualBarrierBehavior : TowerBehavior
{
    [Header("Configuracao da Barreira")]
    [Tooltip("Percentage of damage to absorb (0.15 = 15%).")]
    public float damageAbsorption = 0.15f;
    public float barrierRadius = 5f;

    /// <summary>
    /// Called by a nearby tower that is taking damage. Returns the reduced damage after absorption.
    /// </summary>
    public float AbsorbDamage(float incomingDamage)
    {
        if (!IsServer) return incomingDamage;
        if (towerController == null) return incomingDamage;

        float absorbedDamage = incomingDamage * damageAbsorption;
        towerController.TakeDamage(absorbedDamage);
        return incomingDamage - absorbedDamage;
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.5f, 0f, 1f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, barrierRadius);
    }
}
