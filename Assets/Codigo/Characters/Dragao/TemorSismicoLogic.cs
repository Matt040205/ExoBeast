using UnityEngine;

public class TemorSismicoLogic : MonoBehaviour
{
    // Adicionado parametros: vulnMultiplier e vulnDuration
    public void Setup(GameObject owner, float range, float angle, float damage, float duration, float upForce, float vulnMultiplier, float vulnDuration)
    {
        // Detecta inimigos na área
        Collider[] hits = Physics.OverlapSphere(transform.position, range);

        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                // Calcula direção para checar o cone (ângulo)
                Vector3 dirToEnemy = (hit.transform.position - transform.position).normalized;

                // Verifica se está dentro do ângulo
                if (Vector3.Angle(transform.forward, dirToEnemy) < angle / 2)
                {
                    EnemyHealthSystem hp = hit.GetComponent<EnemyHealthSystem>();

                    // 1. Aplica Dano e Vulnerabilidade
                    if (hp != null)
                    {
                        hp.TakeDamage(damage);

                        // APLICAÇÃO DA VULNERABILIDADE (Debuff)
                        if (vulnMultiplier > 1f)
                        {
                            hp.AplicarVulnerabilidadeTemporaria(vulnMultiplier, vulnDuration);
                        }
                    }

                    // 2. Aplica Controle (Knockup + Slow)
                    EnemyController ai = hit.GetComponent<EnemyController>();
                    if (ai != null)
                    {
                        ai.ApplyKnockback(Vector3.up, upForce);
                        ai.ApplySlow(1f, duration);
                    }
                }
            }
        }

        Destroy(gameObject, 2f);
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 5f);
    }
}