using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Dá à torre do Dragão a capacidade física de patrulhar 
/// num vasto raio em torno do ponto de spawn, detectando inimigos 
/// de longe e caminhando até o curto alcance Melee.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class DragonPatrolBehavior : MonoBehaviour
{
    [Header("Movimentação")]
    public float moveSpeed = 4f;
    public float patrolVisionRadius = 15f; 

    private Vector3 homePosition;
    private NavMeshAgent agent;
    private TowerController tower;

    void Start()
    {
        tower = GetComponent<TowerController>();
        agent = GetComponent<NavMeshAgent>();
        homePosition = transform.position;

        if (agent != null)
        {
            agent.speed = moveSpeed;
            // Para antes de encostar, pra bater no range
            agent.stoppingDistance = (tower != null ? tower.CurrentRange * 0.8f : 1.5f); 
        }
    }

    void Update()
    {
        if (tower == null || agent == null || !agent.isOnNavMesh) return;

        Transform chaseTarget = tower.TargetEnemy;

        // Se o Tower nao achou ninguem porque esta muito longe, o Radar de Patrulha procura
        if (chaseTarget == null)
        {
            Collider[] colliders = Physics.OverlapSphere(homePosition, patrolVisionRadius);
            float shortestDist = Mathf.Infinity;

            foreach (var col in colliders)
            {
                if (col.CompareTag("Enemy"))
                {
                    EnemyHealthSystem ehs = col.GetComponent<EnemyHealthSystem>();
                    if (ehs != null && ehs.isDead) continue; 

                    float d = Vector3.Distance(homePosition, col.transform.position);
                    if (d < shortestDist)
                    {
                        shortestDist = d;
                        chaseTarget = col.transform;
                    }
                }
            }
        }

        if (chaseTarget != null)
        {
            // Se o inimigo fugiu ou a torre perseguiu pra fora do raio MAXIMO
            float distToHome = Vector3.Distance(homePosition, chaseTarget.position);
            if (distToHome > patrolVisionRadius * 1.5f) 
            {
                agent.SetDestination(homePosition);
            }
            else
            {
                agent.SetDestination(chaseTarget.position);
            }
        }
        else
        {
            agent.SetDestination(homePosition);
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Application.isPlaying ? homePosition : transform.position, patrolVisionRadius);
    }
}
