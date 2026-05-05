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
            // Validação de NavMesh: Se a torre foi construída em um Node sem NavMesh (ex: quadrado rosa fora da malha)
            // o agente iria teleportar pro ponto válido mais próximo (ex: o rio/rua).
            // Para evitar o teleporte bizarro, desligamos o agente se não houver NavMesh perto.
            NavMeshHit hit;
            if (NavMesh.SamplePosition(homePosition, out hit, 2.0f, NavMesh.AllAreas))
            {
                agent.Warp(hit.position);
                agent.speed = moveSpeed;
                agent.stoppingDistance = (tower != null ? tower.CurrentRange * 0.8f : 1.5f); 
            }
            else
            {
                Debug.LogWarning($"[DragonPatrol] Construído muito longe do NavMesh! Desativando patrulha na torre {gameObject.name}.");
                agent.enabled = false;
            }
        }
    }

    void Update()
    {
        if (tower == null || agent == null || !agent.isOnNavMesh) return;

        Transform chaseTarget = tower.TargetEnemy;

        // Se o alvo morreu ou sumiu, anula para nao seguir para o Pooling
        if (chaseTarget != null)
        {
            EnemyHealthSystem ehsTarget = chaseTarget.GetComponent<EnemyHealthSystem>();
            if (ehsTarget != null && ehsTarget.isDead)
            {
                chaseTarget = null;
            }
            else if (!chaseTarget.gameObject.activeInHierarchy)
            {
                chaseTarget = null;
            }
        }

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

                    // Ignorar inimigos voadores se a torre não puder atingi-los
                    if (tower != null && !tower.TargetsFlyingEnemies)
                    {
                        EnemyController ec = col.GetComponent<EnemyController>();
                        if (ec != null && ec.enemyData != null && ec.enemyData.enemyType == EnemyType.Voador)
                        {
                            continue;
                        }
                    }

                    float d = Vector3.Distance(homePosition, col.transform.position);
                    if (d < shortestDist)
                    {
                        shortestDist = d;
                        chaseTarget = col.transform;
                    }
                }
            }
        }

        // Deixa a TowerController cuidar da rotação fina se tiver alvo
        agent.updateRotation = (chaseTarget == null);

        float dragonDistToHome = Vector3.Distance(homePosition, transform.position);

        if (chaseTarget != null)
        {
            // Se o dragao foi longe demais da base, forca a volta
            if (dragonDistToHome > patrolVisionRadius) 
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
            // Voltar base
            if (dragonDistToHome > 0.5f)
            {
                agent.SetDestination(homePosition);
            }
            else
            {
                agent.ResetPath(); // Para evitar andar no mesmo lugar
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(Application.isPlaying ? homePosition : transform.position, patrolVisionRadius);
    }
}
