using UnityEngine;
using Unity.Netcode;
using ExoBeasts.Multiplayer.Sync;

public class DragonShockwaveBehavior : TowerBehavior
{
    [Header("ConfiguraÃ§Ãµes da Onda de Choque")]
    public GameObject shockwaveVFX;
    public float shockwaveRadius = 3f;
    public float shockwaveDamagePct = 0.5f;
    public float slowPercentage = 0.3f;
    public float slowDuration = 2f;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;

        if (towerController != null)
            towerController.OnTargetDamaged += HandleTargetHit;
    }

    private void HandleTargetHit(EnemyHealthSystem target)
    {
        NetworkGameplayResolver.TryResolveAttackerFromBuilding(this, out ulong attackerClientId, out PlayerHealthSystem attackerHealth);
        Vector3 position = target.transform.position;

        if (shockwaveVFX != null)
            Instantiate(shockwaveVFX, position, Quaternion.identity);

        Collider[] hits = Physics.OverlapSphere(position, shockwaveRadius);
        float damageToDeal = towerController.CurrentDamage * shockwaveDamagePct;

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Enemy"))
                continue;

            EnemyHealthSystem hp = hit.GetComponent<EnemyHealthSystem>();
            if (hp != null && !hp.isDead && hp.gameObject != target.gameObject)
                hp.ApplyAuthoritativeDamage(damageToDeal, 0f, false, attackerClientId, attackerHealth);

            EnemyController enemy = hit.GetComponent<EnemyController>();
            if (enemy != null)
                enemy.ApplySlow(slowPercentage, slowDuration);
        }
    }

    private void OnDestroy()
    {
        if (towerController != null)
            towerController.OnTargetDamaged -= HandleTargetHit;
    }
}
