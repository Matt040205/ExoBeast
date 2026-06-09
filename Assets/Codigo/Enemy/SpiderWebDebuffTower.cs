using UnityEngine;
using UnityEngine.AI;
using System.Collections;

public class SpiderWebDebuffTower : MonoBehaviour
{
    private TowerController tower;
    private DragonPatrolBehavior dragonPatrol;
    private NavMeshAgent agent;

    private int hitCount = 0;
    private float slowTimer = 0f;
    private const float SLOW_DURATION = 3f;

    private bool isTrapped = false;
    private Coroutine debuffCoroutine;

    private void Awake()
    {
        tower = GetComponent<TowerController>();
        dragonPatrol = GetComponent<DragonPatrolBehavior>();
        agent = GetComponent<NavMeshAgent>();
    }

    public void OnHit(bool enableTrap)
    {
        if (tower == null) return;
        if (isTrapped) return;

        hitCount++;
        slowTimer = SLOW_DURATION;

        // Reduz a velocidade de ataque da torre para 50%
        tower.attackSpeedMultiplier = 0.5f;

        // Se for a torre Dragão que se move, reduz a velocidade de movimento para 50%
        if (dragonPatrol != null && agent != null && agent.enabled)
        {
            agent.speed = dragonPatrol.moveSpeed * 0.5f;
        }

        // Verifica se ativou o aprisionamento (5 hits)
        if (hitCount >= 5 && enableTrap)
        {
            TrapTower();
        }
        else
        {
            if (debuffCoroutine != null) StopCoroutine(debuffCoroutine);
            debuffCoroutine = StartCoroutine(SlowCountdown());
        }
    }

    private void TrapTower()
    {
        isTrapped = true;

        if (debuffCoroutine != null) StopCoroutine(debuffCoroutine);
        debuffCoroutine = StartCoroutine(TrapTimer());
    }

    private IEnumerator TrapTimer()
    {
        // 1. Aplica o aprisionamento de 2 segundos
        if (dragonPatrol != null && agent != null && agent.enabled)
        {
            // Se for Dragão, fica completamente parado
            agent.isStopped = true;
            agent.speed = 0f;
        }
        else
        {
            // Se for torre normal, para de atacar (velocidade de ataque = 0)
            tower.attackSpeedMultiplier = 0f;
        }

        yield return new WaitForSeconds(2f);

        // 2. Liberta a torre e restaura os status normais
        ReleaseTower();
    }

    private void ReleaseTower()
    {
        isTrapped = false;
        hitCount = 0;

        if (tower != null)
        {
            tower.attackSpeedMultiplier = 1f;
        }

        if (dragonPatrol != null && agent != null && agent.enabled)
        {
            agent.isStopped = false;
            agent.speed = dragonPatrol.moveSpeed;
        }

        Destroy(this);
    }

    private IEnumerator SlowCountdown()
    {
        while (slowTimer > 0)
        {
            slowTimer -= Time.deltaTime;
            yield return null;
        }

        if (!isTrapped)
        {
            hitCount = 0;
            if (tower != null)
            {
                tower.attackSpeedMultiplier = 1f;
            }
            if (dragonPatrol != null && agent != null && agent.enabled)
            {
                agent.speed = dragonPatrol.moveSpeed;
            }
            Destroy(this);
        }
    }

    private void OnDestroy()
    {
        // Garante restauração caso o componente seja limpo de outra forma
        if (tower != null)
        {
            tower.attackSpeedMultiplier = 1f;
        }
        if (dragonPatrol != null && agent != null && agent.enabled)
        {
            agent.isStopped = false;
            agent.speed = dragonPatrol.moveSpeed;
        }
    }
}
