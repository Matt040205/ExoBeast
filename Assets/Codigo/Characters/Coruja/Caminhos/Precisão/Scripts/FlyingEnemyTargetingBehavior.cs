using UnityEngine;

/// <summary>
/// ── FlyingEnemyTargetingBehavior ─────────────────────────
/// Enables the tower to target airborne enemies (Precisão Nível 3).
///
///  ▸ Seta TargetsFlyingEnemies = true no Initialize, sem guard de IsServer,
///    pois UpdateTarget() roda localmente em todos os contextos (MonoBehaviour).
///  ▸ OnDestroy reseta o flag para que a torre pare de mirar voadores se o behavior for removido.
/// ─────────────────────────────────────────────────────────
/// </summary>
public class FlyingEnemyTargetingBehavior : TowerBehavior
{
    private TowerController towerOwner;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);

        towerOwner = owner;
        if (owner != null)
            owner.TargetsFlyingEnemies = true;
    }

    private void OnDestroy()
    {
        if (towerOwner != null)
            towerOwner.TargetsFlyingEnemies = false;
    }
}
