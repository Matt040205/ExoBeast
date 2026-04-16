using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class SupportAuraBehavior : TowerBehavior
{
    public float baseAuraRange = 5f;
    public float healTickRate = 1f;
    public LayerMask allyLayer;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        // Default Layers format
        if (allyLayer.value == 0) allyLayer = LayerMask.GetMask("Player", "Tower");

        StartCoroutine(SupportAuraRoutine());
    }

    private IEnumerator SupportAuraRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(healTickRate);
            if (towerController == null) yield break;

            Collider[] allies = Physics.OverlapSphere(transform.position, baseAuraRange, allyLayer);
            foreach (var ally in allies)
            {
                if (ally.gameObject != gameObject)
                {
                    PolvoSupportHelper.ApplyHealAndBuff(ally.gameObject, towerController);
                }
            }
        }
    }
}
