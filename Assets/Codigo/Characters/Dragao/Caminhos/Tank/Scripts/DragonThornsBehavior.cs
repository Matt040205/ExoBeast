using UnityEngine;
using Unity.Netcode;

public class DragonThornsBehavior : TowerBehavior
{
    [Header("Configurações do Espinho")]
    public float damageReflectionPercent = 0.20f;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
        {
            towerController.OnDamageTaken += HandleDamageTaken;
        }
    }

    private void HandleDamageTaken(float damage, Transform attacker)
    {
        if (attacker != null)
        {
            EnemyHealthSystem enemyHealth = attacker.GetComponent<EnemyHealthSystem>();
            if (enemyHealth != null && !enemyHealth.isDead)
            {
                float damageToReflect = damage * damageReflectionPercent;
                // Devolve dano sem depender de animação usando HitScan nativo do inimigo
                enemyHealth.TakeDamage(damageToReflect);
                
                // Opcional: Instanciar particula de Thorns no alvo
            }
        }
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnDamageTaken -= HandleDamageTaken;
        }
    }
}
