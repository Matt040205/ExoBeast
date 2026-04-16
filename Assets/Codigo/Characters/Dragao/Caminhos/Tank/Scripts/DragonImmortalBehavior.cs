using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class DragonImmortalBehavior : TowerBehavior
{
    [Header("Configurações de Imortalidade")]
    public float invulnerableDuration = 5f;
    public float tauntRadius = 15f;
    
    private int lastTriggeredWave = -1;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
        {
            towerController.OnFatalDamagePrevented += HandleFatalDamage;
        }
    }

    private bool HandleFatalDamage()
    {
        if (HordeManager.Instance != null)
        {
            int currentWave = HordeManager.Instance.currentHorde.Value;
            if (currentWave == lastTriggeredWave)
            {
                return false; // Já ativou nessa rodada, deixe morrer
            }
            lastTriggeredWave = currentWave;
        }

        // Fica invulnerável e aciona Taunt em massa
        towerController.IsInvulnerable = true;
        MassTaunt();
        StartCoroutine(InvulnerabilityRoutine());

        return true; // Verdadeiro = "Consegui prevenir a morte!"
    }

    private void MassTaunt()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, tauntRadius);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyController enemy = hit.GetComponent<EnemyController>();
                if (enemy != null)
                {
                    enemy.ApplyTaunt(transform, invulnerableDuration);
                }
            }
        }
    }

    private IEnumerator InvulnerabilityRoutine()
    {
        yield return new WaitForSeconds(invulnerableDuration);
        if (towerController != null)
        {
            towerController.IsInvulnerable = false;
        }
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnFatalDamagePrevented -= HandleFatalDamage;
        }
    }
}
