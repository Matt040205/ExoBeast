using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class DragonShieldGeneratorBehavior : TowerBehavior
{
    [Header("Configurações de Escudo")]
    public float auraRange = 10f;
    public LayerMask allyLayer;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;
        if (allyLayer.value == 0) allyLayer = LayerMask.GetMask("Player", "Tower");
        StartCoroutine(ShieldRoutine());
    }

    private IEnumerator ShieldRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(10f); // Gera escudo a cada 10s
            if (towerController == null) yield break;

            float shieldAmount = towerController.GetComponent<DragonShieldBoostBehavior>() != null ? 100f : 50f;
            bool canExplode = towerController.GetComponent<DragonShieldExplosionBehavior>() != null;

            Collider[] allies = Physics.OverlapSphere(transform.position, auraRange, allyLayer);
            foreach (var ally in allies)
            {
                if (ally.gameObject != gameObject) // Não aplica escudo a ela mesma
                {
                    AllyShield shield = ally.GetComponent<AllyShield>();
                    if (shield == null) shield = ally.gameObject.AddComponent<AllyShield>();
                    
                    shield.ApplyShield(shieldAmount, towerController, canExplode);
                }
            }
        }
    }
}
