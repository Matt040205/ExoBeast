using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── PaintSlipBehavior ──────────────────────────────────────
/// Grants a chance to make the enemy slip (erring attacks) on hit.
/// ───────────────────────────────────────────────────────────
/// </summary>
public class PaintSlipBehavior : TowerBehavior
{
    [Header("Configurações do Escorregão")]
    [Tooltip("Chance de escorregar ao ser atingido (0.15 = 15%)")]
    [Range(0f, 1f)]
    public float slipChance = 0.15f;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
        {
            towerController.OnTargetDamaged += HandleTargetHit;
        }
    }

    private void HandleTargetHit(EnemyHealthSystem target)
    {
        if (Random.value <= slipChance)
        {
            EnemyController enemy = target.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.ApplySlip();
            }
        }
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnTargetDamaged -= HandleTargetHit;
        }
    }
}
