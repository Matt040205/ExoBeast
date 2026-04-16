using UnityEngine;
using Unity.Netcode;

public class DragonStunBehavior : TowerBehavior
{
    [Header("Configurações de Stun")]
    [Tooltip("Exemplo: 0.2 para 20%")]
    public float stunChance = 0.2f;
    public float stunDuration = 0.5f;

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
        if (Random.value <= stunChance)
        {
            EnemyController enemy = target.GetComponent<EnemyController>();
            if (enemy != null)
            {
                enemy.ApplyStun(stunDuration);
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
