using UnityEngine;
using Unity.Netcode;

public class DragonShockwaveBehavior : TowerBehavior
{
    [Header("Configurações da Onda de Choque")]
    public GameObject shockwaveVFX;
    public float shockwaveRadius = 3f;
    public float shockwaveDamagePct = 0.5f;
    public float slowPercentage = 0.3f;
    public float slowDuration = 2f;

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
        Vector3 position = target.transform.position;

        if (shockwaveVFX != null)
        {
            Instantiate(shockwaveVFX, position, Quaternion.identity);
        }

        Collider[] hits = Physics.OverlapSphere(position, shockwaveRadius);
        float damageToDeal = towerController.CurrentDamage * shockwaveDamagePct;

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyHealthSystem hp = hit.GetComponent<EnemyHealthSystem>();
                if (hp != null && !hp.isDead)
                {
                    // Evita reaplicar o hit no próprio alvo principal se não quiser, 
                    // mas o texto diz 'ao redor do alvo', geralmente atinge a todos.
                    if (hp.gameObject != target.gameObject)
                        hp.TakeDamage(damageToDeal);

                    // Lentidão em área
                    EnemyController enemy = hit.GetComponent<EnemyController>();
                    if (enemy != null)
                    {
                        enemy.ApplySlow(slowPercentage, slowDuration);
                    }
                }
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
