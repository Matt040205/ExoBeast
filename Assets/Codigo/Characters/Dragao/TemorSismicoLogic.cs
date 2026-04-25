using UnityEngine;
using Unity.Netcode;

/// <summary>
/// â”€â”€ TemorSismicoLogic â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
/// NetworkBehaviour spawnado pelo servidor ao usar Temor Sismico (Q do Dragao).
///
///  â–¸ Server: aplica dano, vulnerabilidade e knockback em inimigos no cone
///  â–¸ Todos os clientes: veem o VFX (particulas no prefab) via NGO spawn
///  â–¸ Destruido apos 2s automaticamente no servidor
/// â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€â”€
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class TemorSismicoLogic : NetworkBehaviour
{
    private float _range;
    private float _angle;
    private float _damage;
    private float _knockUpDuration;
    private float _knockUpForce;
    private float _vulnMultiplier;
    private float _vulnDuration;
    private bool _setupReady;

    public void Setup(GameObject owner, float range, float angle, float damage,
        float knockUpDuration, float knockUpForce, float vulnMultiplier, float vulnDuration)
    {
        _range = range;
        _angle = angle;
        _damage = damage;
        _knockUpDuration = knockUpDuration;
        _knockUpForce = knockUpForce;
        _vulnMultiplier = vulnMultiplier;
        _vulnDuration = vulnDuration;
        _setupReady = true;

        // Posicionar no owner (chamado antes do Spawn, transform jÃ¡ foi definido no Instantiate)
        transform.position = owner.transform.position;
        transform.rotation = AbilityAimUtility.ResolveAimRotation(owner);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (!IsServer) return;

        if (_setupReady)
            ApplyEffects();

        Invoke(nameof(DespawnSelf), 2f);
    }

    private void ApplyEffects()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, _range);

        foreach (var hit in hits)
        {
            if (!hit.CompareTag("Enemy")) continue;

            Vector3 dirToEnemy = (hit.transform.position - transform.position).normalized;
            if (Vector3.Angle(transform.forward, dirToEnemy) >= _angle / 2f) continue;

            EnemyHealthSystem hp = hit.GetComponent<EnemyHealthSystem>();
            if (hp != null)
            {
                hp.TakeDamage(_damage);

                if (_vulnMultiplier > 1f)
                    hp.AplicarVulnerabilidadeTemporaria(_vulnMultiplier, _vulnDuration);
            }

            EnemyController ai = hit.GetComponent<EnemyController>();
            if (ai != null)
            {
                ai.ApplyKnockback(Vector3.up, _knockUpForce);
                ai.ApplySlow(1f, _knockUpDuration);
            }
        }
    }

    private void DespawnSelf()
    {
        if (IsServer && NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }

    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, 5f);
    }
}
