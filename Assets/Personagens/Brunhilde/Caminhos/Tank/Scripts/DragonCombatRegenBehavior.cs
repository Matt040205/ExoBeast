using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class DragonCombatRegenBehavior : TowerBehavior
{
    [Header("Configurações de Regen")]
    public float regenPercentPerSecond = 0.05f;
    public float combatDuration = 5f; 
    
    private Coroutine regenCoroutine;
    private float combatTimer = 0f;

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
        combatTimer = combatDuration;
        if (regenCoroutine == null)
        {
            regenCoroutine = StartCoroutine(RegenRoutine());
        }
    }

    private IEnumerator RegenRoutine()
    {
        while (combatTimer > 0)
        {
            yield return new WaitForSeconds(1f);
            if (towerController != null && !towerController.IsDestroyed)
            {
                float healAmount = towerController.MaxHealth * regenPercentPerSecond;
                towerController.Heal(healAmount);
            }
            combatTimer -= 1f;
        }
        regenCoroutine = null;
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnDamageTaken -= HandleDamageTaken;
        }
    }
}
