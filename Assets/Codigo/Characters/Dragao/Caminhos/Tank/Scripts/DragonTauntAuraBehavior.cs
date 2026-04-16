using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class DragonTauntAuraBehavior : TowerBehavior
{
    [Header("Configurações do Taunt")]
    public float tauntRadius = 15f;
    public float tauntDuration = 2f;
    public float tauntInterval = 5f;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;
        StartCoroutine(TauntRoutine());
    }

    private IEnumerator TauntRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(tauntInterval);
            if (towerController == null || towerController.IsDestroyed) yield break;

            Collider[] hits = Physics.OverlapSphere(transform.position, tauntRadius);
            foreach (var hit in hits)
            {
                if (hit.CompareTag("Enemy"))
                {
                    EnemyController enemy = hit.GetComponent<EnemyController>();
                    if (enemy != null)
                    {
                        enemy.ApplyTaunt(transform, tauntDuration);
                    }
                }
            }
        }
    }
}
