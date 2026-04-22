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

    [Header("Efeitos Visuais")]
    public GameObject attackVfxPrefab;

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

            if (towerAuraCoroutine != null) StopCoroutine(towerAuraCoroutine);
            towerAuraCoroutine = StartCoroutine(TowerAuraCycle());
        }
    }

    void Update()
    {
        if (!IsServer || enemyController == null || enemyController.IsDead || enemyData == null) return;

        // Recupera o alvo do controlador
        Transform currentTarget = enemyController.Target;
        
        if (currentTarget != null && currentTarget.CompareTag("Player"))
        {
            float dist = Vector3.Distance(transform.position, currentTarget.position);
            
            // Se está no range de atacar
            if (dist <= enemyController.attackDistance)
            {
                if (attackCoroutine == null)
                {
                    attackCoroutine = StartCoroutine(PlayerAttackCycleStateDriven());
                }
            }
            else
            {
                if (attackCoroutine != null)
                {
                    StopCoroutine(attackCoroutine);
                    attackCoroutine = null;
                }
            }
        }
        else
        {
            if (attackCoroutine != null)
            {
                StopCoroutine(attackCoroutine);
                attackCoroutine = null;
            }
        }
    }

    private IEnumerator PlayerAttackCycleStateDriven()
    {
        yield return new WaitForSeconds(timeToDamage);

        while (enemyController != null && !enemyController.IsDead && enemyController.Target != null)
        {
            float dist = Vector3.Distance(transform.position, enemyController.Target.position);
            if (dist > enemyController.attackDistance) break;

            TriggerAttackVfx(enemyController.Target.position);
            ProcessAttack(enemyController.Target);

            float cooldown = (enemyData != null && enemyData.attackSpeed > 0) ? (1f / enemyData.attackSpeed) : 1f;
            yield return new WaitForSeconds(cooldown);
        }
        
        attackCoroutine = null;
    }

    private void TriggerAttackVfx(Vector3 targetPos)
    {
        if (attackVfxPrefab == null) return;
        
        Vector3 origin = attackPoint != null ? attackPoint.position : transform.position;
        Vector3 dir = (targetPos - origin).normalized;
        if (dir == Vector3.zero) dir = transform.forward;
        
        // Em rede, invoca o ClientRpc para exibir o VFX em todos
        var nwEnemy = GetComponent<ExoBeasts.Multiplayer.Sync.NetworkedEnemy>();
        if (nwEnemy != null && nwEnemy.IsSpawned)
            nwEnemy.PlayAttackVfxClientRpc(origin, Quaternion.LookRotation(dir));
        else
        {
            // Offline ou teste local
            GlobalVFXPool.GetVFX(attackVfxPrefab, origin, Quaternion.LookRotation(dir), 2f);
        }
    }

    private void ProcessAttack(Transform targetTransform)
    {
        if (enemyData.enemyType == EnemyType.Voador)
        {
            // ATAQUE RANGED (Hitscan)
            Vector3 origin = attackPoint != null ? attackPoint.position : transform.position;
            Vector3 dir = (targetTransform.position - origin).normalized;
            
            // Atira o raycast até a distância máxima que ele conseguiria ver
            if (Physics.Raycast(origin, dir, out RaycastHit hit, enemyController.loseSightDistance, playerLayer))
            {
                PlayerHealthSystem playerHealth = hit.collider.GetComponent<PlayerHealthSystem>();
                if (playerHealth != null)
                {
                    // No futuro: se você criar efeitos, chame um ClientRpc(origin, hit.point) aqui
                    playerHealth.TakeDamage(currentDamage, transform);
                }
            }
        }
        else
        {
            // ATAQUE CORPO A CORPO (Área)
            ApplyDamageInArea();
        }
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
                }
            }
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
