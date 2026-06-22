using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── PaintKnockbackBehavior ─────────────────────────────────
/// Empurra levemente os inimigos para trs ao serem atingidos.
/// ───────────────────────────────────────────────────────────
/// </summary>
public class PaintKnockbackBehavior : TowerBehavior
{
    [Header("Configuraes de Knockback")]
    public float knockbackForce = 5f;

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
            Vector3 dir = (enemy.transform.position - transform.position).normalized;
            enemy.ApplyKnockback(dir + Vector3.up * 0.2f, knockbackForce);
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
