using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── DoubleAttackBehavior ─────────────────────────────────
/// Gives the tower a chance to attack a second time immediately.
///
///  ▸ Server-only: extra attack only fired on the authoritative server
///  ▸ Probability roll via Random.value each time a target takes damage
///  ▸ Cleanup in OnDestroy prevents dangling event references
/// ─────────────────────────────────────────────────────────
/// </summary>
public class DoubleAttackBehavior : TowerBehavior
{
    [Header("Configuracao do Ataque Duplo")]
    [Tooltip("Probability of a second attack (0.25 = 25%).")]
    [Range(0f, 1f)]
    public float chance = 0.25f;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
        {
            towerController.OnTargetDamaged += TryDoubleAttack;
        }
    }

    private void TryDoubleAttack(EnemyHealthSystem target)
    {
        if (Random.value <= chance)
        {
            towerController.PerformExtraAttack();
        }
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnTargetDamaged -= TryDoubleAttack;
        }
    }
}
