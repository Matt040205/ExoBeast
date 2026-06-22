using UnityEngine;
using Unity.Netcode;

public class DragonArmorShredBehavior : TowerBehavior
{
    [Header("Configurações de Armadura")]
    public float shredPercentage = 0.3f;
    public float shredDuration = 3f;

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
        // Usa o ApplyTemporaryArmorShred no próprio target.
        target.ApplyTemporaryArmorShred(shredPercentage, shredDuration);
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnTargetDamaged -= HandleTargetHit;
        }
    }
}
