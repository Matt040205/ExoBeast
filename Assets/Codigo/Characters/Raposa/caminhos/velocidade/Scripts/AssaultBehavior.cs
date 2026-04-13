using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── AssaultBehavior ──────────────────────────────────────
/// Cumulatively boosts attack speed on critical hits; resets after a timer.
///
///  ▸ Server-only: attack-speed mutations only run on server authority
///  ▸ Each new critical resets the full buff duration (not per-stack)
///  ▸ ResetBuff removes the total accumulated bonus in one call
/// ─────────────────────────────────────────────────────────
/// </summary>
public class AssaultBehavior : TowerBehavior
{
    [Header("Configuracoes do Assalto")]
    [Tooltip("Attack speed bonus per stack. 0.1 = 10%.")]
    public float attackSpeedBonus = 0.1f;
    public int maxStacks = 3;
    [Tooltip("How long the buff lasts in seconds.")]
    public float buffDuration = 5f;

    private int currentStacks;
    private float buffTimer;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
        {
            towerController.OnCriticalHit += HandleCriticalHit;
        }
    }

    private void Update()
    {
        if (!IsServer) return;

        if (buffTimer > 0)
        {
            buffTimer -= Time.deltaTime;
            if (buffTimer <= 0)
            {
                ResetBuff();
            }
        }
    }

    private void HandleCriticalHit(EnemyHealthSystem target)
    {
        if (currentStacks < maxStacks)
        {
            currentStacks++;
            towerController.AddAttackSpeedBonus(attackSpeedBonus);
        }

        // Every new critical resets the full buff window
        buffTimer = buffDuration;
    }

    private void ResetBuff()
    {
        towerController.AddAttackSpeedBonus(-(currentStacks * attackSpeedBonus));
        currentStacks = 0;
        buffTimer = 0f;
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnCriticalHit -= HandleCriticalHit;
            if (currentStacks > 0)
            {
                ResetBuff();
            }
        }
    }
}
