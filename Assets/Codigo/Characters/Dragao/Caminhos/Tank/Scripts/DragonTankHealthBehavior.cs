using UnityEngine;
using Unity.Netcode;

public class DragonTankHealthBehavior : TowerBehavior
{
    [Header("Configurações")]
    public float healthBonusPercent = 0.3f;
    private float appliedHealthBonus = 0f;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
        {
            appliedHealthBonus = towerController.MaxHealth * healthBonusPercent;
            towerController.AddMaxHealth(appliedHealthBonus);
        }
    }

    private void OnDestroy()
    {
        if (towerController != null && appliedHealthBonus > 0)
        {
            towerController.AddMaxHealth(-appliedHealthBonus);
        }
    }
}
