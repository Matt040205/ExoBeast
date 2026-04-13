using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ── PreyMarkLogic ────────────────────────────────────────
/// Aplica "marcado" a todos os inimigos em rede via NGO SpawnManager.
///
///  ▸ StartEffect (server-only): itera SpawnedObjects, chama ApplyMarkedStatus
///  ▸ ApplyMarkedStatusOnServer: coroutine aguarda markDuration, remove efeito
///  ▸ Despawn automatico ao fim da coroutine (IsSpawned guard)
/// ─────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class PreyMarkLogic : NetworkBehaviour
{
    private float markDuration;
    private float bonusDamageMultiplier;

    public void StartEffect(float duration, float damageBonus, CommanderAbilityController abilityController, Ability sourceAbility)
    {
        if (!IsServer) return;

        this.markDuration = duration;
        this.bonusDamageMultiplier = damageBonus;

        if (abilityController != null)
        {
            // Nota: SetAbilityUsage deve ser validado/propagado pelo servidor
            // No momento assume-se que o CommanderAbilityController no servidor esta em sincronia
        }

        StartCoroutine(ApplyMarkedStatusOnServer());
    }

    private IEnumerator ApplyMarkedStatusOnServer()
    {
        var spawnedObjects = NetworkManager.Singleton.SpawnManager.SpawnedObjects;
        List<EnemyHealthSystem> markedEnemies = new List<EnemyHealthSystem>();

        foreach (var entry in spawnedObjects)
        {
            if (entry.Value.CompareTag("Enemy") || entry.Value.GetComponent<EnemyHealthSystem>() != null)
            {
                var health = entry.Value.GetComponent<EnemyHealthSystem>();
                if (health != null)
                {
                    health.ApplyMarkedStatus(bonusDamageMultiplier);
                    markedEnemies.Add(health);
                }
            }
        }


        yield return new WaitForSeconds(markDuration);

        foreach (var enemy in markedEnemies)
        {
            if (enemy != null)
            {
                enemy.ApplyMarkedStatus(1.0f);
            }
        }

        if (NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }
}
