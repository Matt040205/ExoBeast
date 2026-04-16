using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class DragonArmorAuraBehavior : TowerBehavior
{
    [Header("Configurações da Aura")]
    public float auraRange = 10f;
    public LayerMask allyLayer;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;
        if (allyLayer.value == 0) allyLayer = LayerMask.GetMask("Player", "Tower");
        StartCoroutine(AuraRoutine());
    }

    private IEnumerator AuraRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(0.5f);
            if (towerController == null) yield break;

            // Nível 1: Armadura
            float armB = 0.10f; 
            // Nível 3: Damage Reduction
            float drB = towerController.GetComponent<DragonDamRedAuraBehavior>() != null ? 0.15f : 0f;

            Collider[] allies = Physics.OverlapSphere(transform.position, auraRange, allyLayer);
            foreach (var ally in allies)
            {
                if (ally.gameObject != gameObject)
                {
                    DragonAuraBuff buff = ally.GetComponent<DragonAuraBuff>();
                    if (buff == null) buff = ally.gameObject.AddComponent<DragonAuraBuff>();
                    
                    TowerController tc = ally.GetComponent<TowerController>();
                    buff.RefreshBuff(tc, armB, drB, 1.0f);
                }
            }
        }
    }
}
