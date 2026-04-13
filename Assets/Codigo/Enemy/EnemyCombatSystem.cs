using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// ── EnemyCombatSystem ──────────────────────────────────
/// Sistema de combate do inimigo (server-only).
///
///  ▸ Server: detecta jogadores via trigger, aplica dano ciclico
///  ▸ Server: aura de dano em torres via OverlapSphere periodico
///  ▸ Remotos: script desativado (IA roda apenas no servidor)
/// ─────────────────────────────────────────────────────
/// </summary>
public class EnemyCombatSystem : NetworkBehaviour
{
    [Header("Configurações de Combate (Player)")]
    public float attackRange = 2f;
    public float timeToDamage = 2f;

    [Header("Aura de Dano em Torres")]
    public float towerAuraRadius = 10f;
    public float towerAuraDamage = 14f;
    public float towerAuraInterval = 3f;

    [Header("Referências")]
    public Transform attackPoint;
    public LayerMask playerLayer;
    public LayerMask towerLayer;

    private EnemyController enemyController;
    private EnemyDataSO enemyData;
    private float currentDamage;

    private bool playerIsInside = false;
    private Coroutine attackCoroutine;
    private Coroutine towerAuraCoroutine;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        enemyController = GetComponent<EnemyController>();
        
        // APENAS o servidor deve processar a IA de combate em rede
        if (!IsServer)
        {
            this.enabled = false;
            return;
        }
    }

    public void InitializeCombat(EnemyDataSO data, int nivel)
    {
        if (!IsServer) return;

        this.enemyData = data;
        if (enemyData != null)
        {
            currentDamage = enemyData.GetDamage(nivel);

            // Tentar encontrar ou adicionar um SphereCollider para o gatilho de detecção
            SphereCollider sphereCollider = (attackPoint != null && attackPoint.GetComponent<SphereCollider>() != null)
                ? attackPoint.GetComponent<SphereCollider>()
                : GetComponent<SphereCollider>();

            if (sphereCollider == null)
            {
                sphereCollider = gameObject.AddComponent<SphereCollider>();
            }

            if (sphereCollider != null)
            {
                sphereCollider.isTrigger = true;
                sphereCollider.radius = attackRange;
            }

            if (towerAuraCoroutine != null) StopCoroutine(towerAuraCoroutine);
            towerAuraCoroutine = StartCoroutine(TowerAuraCycle());
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (!IsServer) return;

        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            if (!playerIsInside)
            {
                playerIsInside = true;
                if (attackCoroutine != null) StopCoroutine(attackCoroutine);
                attackCoroutine = StartCoroutine(PlayerAttackCycle());
            }
        }
    }

    void OnTriggerExit(Collider other)
    {
        if (!IsServer) return;

        if (((1 << other.gameObject.layer) & playerLayer) != 0)
        {
            if (playerIsInside)
            {
                playerIsInside = false;
                if (attackCoroutine != null)
                {
                    StopCoroutine(attackCoroutine);
                    attackCoroutine = null;
                }
            }
        }
    }

    private IEnumerator PlayerAttackCycle()
    {
        yield return new WaitForSeconds(timeToDamage);

        while (playerIsInside && enemyController != null && !enemyController.IsDead)
        {
            ApplyDamageInArea();

            float cooldown = (enemyData != null && enemyData.attackSpeed > 0) ? (1f / enemyData.attackSpeed) : 1f;
            yield return new WaitForSeconds(cooldown);
        }
        attackCoroutine = null;
    }

    void ApplyDamageInArea()
    {
        if (!IsServer || enemyData == null) return;

        if (enemyController != null && enemyController.IsBlinded)
        {
            if (Random.value < 0.8f) return;
        }

        Vector3 origin = attackPoint != null ? attackPoint.position : transform.position;
        Collider[] hitPlayers = Physics.OverlapSphere(origin, attackRange, playerLayer);

        if (hitPlayers.Length > 0)
        {
            // Aplica dano em TODOS os jogadores na area (nao apenas o primeiro)
            bool acertouAlguem = false;
            foreach (Collider col in hitPlayers)
            {
                PlayerHealthSystem playerHealth = col.GetComponent<PlayerHealthSystem>();
                if (playerHealth != null)
                {
                    playerHealth.TakeDamage(currentDamage, transform);
                    acertouAlguem = true;
                }
            }
            if (!acertouAlguem) playerIsInside = false;
        }
        else
        {
            playerIsInside = false;
        }
    }

    private IEnumerator TowerAuraCycle()
    {
        yield return null;

        while (enemyController != null && !enemyController.IsDead)
        {
            ApplyAuraDamageToTowers();
            yield return new WaitForSeconds(towerAuraInterval);
        }
    }

    private void ApplyAuraDamageToTowers()
    {
        if (!IsServer) return;
        
        Collider[] hitTowers = Physics.OverlapSphere(transform.position, towerAuraRadius, towerLayer);
        foreach (Collider towerCollider in hitTowers)
        {
            // No modo multiplayer, as torres também devem ser protegidas por IsServer em seus sistemas de vida
            TowerController tower = towerCollider.GetComponent<TowerController>();
            if (tower != null)
            {
                tower.TakeDamage(towerAuraDamage);
            }
        }
    }

    void OnDrawGizmosSelected()
    {
        if (attackPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(attackPoint.position, attackRange);
        }

        Gizmos.color = Color.magenta;
        Gizmos.DrawWireSphere(transform.position, towerAuraRadius);
    }
}
