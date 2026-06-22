using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── PaintSlowBehavior ──────────────────────────────────────
/// Applies a slow debuff to the target when hit by the tower.
/// ───────────────────────────────────────────────────────────
/// </summary>
public class PaintSlowBehavior : TowerBehavior
{
    [Header("Configurações de Lentidão")]
    [Tooltip("Percentual de lentidão (0.2 = 20%, 0.4 = 40%)")]
    public float slowPercentage = 0.2f;
    [Tooltip("Duração da lentidão em segundos")]
    public float slowDuration = 2f;

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
        EnemyController enemy = target.GetComponent<EnemyController>();
        if (enemy != null)
        {
            enemy.ApplySlow(slowPercentage, slowDuration);
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
