using UnityEngine;
using System.Collections;
using Unity.Netcode;

/// <summary>
/// ── PaintBurstBehavior ─────────────────────────────────────
/// Acumula tiros at descarregar uma rajada rpida de disparos.
/// ───────────────────────────────────────────────────────────
/// </summary>
public class PaintBurstBehavior : TowerBehavior
{
    [Header("Configuraes da Rajada (Burst)")]
    public int shotsForBurst = 10;
    public int burstShots = 5;
    public float burstDelay = 0.1f;

    private int burstShotCounter = 0;
    private bool isBursting = false;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
        {
            towerController.OnTargetDamaged += HandleTargetHit;
        }
    }

    private void HandleTargetHit(EnemyHealthSystem target)
    {
        if (!isBursting)
        {
            burstShotCounter++;
            if (burstShotCounter >= shotsForBurst)
            {
                StartCoroutine(PerformBurstRoutine());
                burstShotCounter = 0;
            }
        }
    }

    private IEnumerator PerformBurstRoutine()
    {
        isBursting = true;
        for (int i = 0; i < burstShots; i++)
        {
            yield return new WaitForSeconds(burstDelay);
            if (towerController != null)
            {
                towerController.PerformExtraAttack();
            }
            else
            {
                break;
            }
        }
        isBursting = false;
    }

    private void OnDestroy()
    {
        if (towerController != null)
        {
            towerController.OnTargetDamaged -= HandleTargetHit;
        }
    }
}
