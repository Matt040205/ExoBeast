using UnityEngine;

public class AquiNaoLogic : MonoBehaviour
{
    public void Setup(GameObject owner, float radius, float damage, float force, float stunTime, CommanderAbilityController controller, Ability ability)
    {
        if (controller != null) controller.SetAbilityUsage(ability, true);

        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        int wallMask = LayerMask.GetMask("Default", "Ground", "Terrain", "Wall");

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyHealthSystem hp = hit.GetComponent<EnemyHealthSystem>();
                if (hp != null) hp.TakeDamage(damage);

                Vector3 direction = (hit.transform.position - transform.position).normalized;
                direction.y = 0.2f;

                bool hitWall = Physics.Raycast(hit.transform.position, direction, 2f, wallMask);

                EnemyController enemyAI = hit.GetComponent<EnemyController>();

                if (hitWall)
                {
                    if (enemyAI != null) enemyAI.ApplySlow(1f, stunTime);
                }

                if (enemyAI != null) enemyAI.ApplyKnockback(direction, force);
            }
        }

        Destroy(gameObject, 0.5f);
    }
}