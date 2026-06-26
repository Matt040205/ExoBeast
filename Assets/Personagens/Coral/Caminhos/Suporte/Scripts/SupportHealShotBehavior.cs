using UnityEngine;
using Unity.Netcode;

public class SupportHealShotBehavior : TowerBehavior
{
    public LayerMask allyLayer;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (allyLayer.value == 0) allyLayer = LayerMask.GetMask("Player", "Tower");

        if (towerController != null)
        {
            towerController.OnTargetDamaged += HandleTargetHit;
        }
    }

    private void HandleTargetHit(EnemyHealthSystem target)
    {
        if (towerController.firePoint == null || target == null) return;

        Vector3 enemyPos = target.transform.position;
        float dist = Vector3.Distance(towerController.firePoint.position, enemyPos);
        Vector3 dir = (enemyPos - towerController.firePoint.position).normalized;

        RaycastHit[] hits = Physics.RaycastAll(towerController.firePoint.position, dir, dist, allyLayer);
        foreach (var hit in hits)
        {
            if (hit.collider.gameObject != gameObject)
            {
                PolvoSupportHelper.ApplyHealAndBuff(hit.collider.gameObject, towerController);
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
