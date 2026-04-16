using UnityEngine;
using UnityEngine.AI;

/// <summary>
/// Dá à torre do Dragão a capacidade física de patrulhar 
/// em torno do ponto de spawn em direção ao alvo.
/// </summary>
[RequireComponent(typeof(NavMeshAgent))]
public class DragonPatrolBehavior : MonoBehaviour
{
    public float moveSpeed = 3.5f;
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
            agent.stoppingDistance = 1.5f; // Para ficar colado pro melee
        }
    }

    void Update()
    {
        if (tower == null || agent == null || !agent.isOnNavMesh) return;

        if (tower.TargetEnemy != null)
        {
            float distToHome = Vector3.Distance(homePosition, tower.TargetEnemy.position);
            
            // Só persegue o inimigo se ele estiver dentro do attackRange original
            if (distToHome <= tower.CurrentRange)
            {
                agent.SetDestination(tower.TargetEnemy.position);
            }
            else
            {
                agent.SetDestination(homePosition);
            }
        }
        else
        {
            agent.SetDestination(homePosition);
        }
    }
}
