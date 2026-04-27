using UnityEngine;
using System.Collections;
using ExoBeasts.Multiplayer.Sync;

public class ObraPrimaLogic : MonoBehaviour
{
    private float _damagePerShot;
    private int _shotsCount;
    private float _duration;
    private float _radius;
    private float _silenceDur;
    private Transform _owner;
    private bool _applyDamage = true;
    private ulong _attackerClientId;
    private PlayerHealthSystem _attackerHealth;

    public void StartUltimate(
        GameObject owner,
        float duration,
        int shotsCount,
        float damagePerShot,
        float radius,
        float silenceDur,
        bool applyDamage = true)
    {
        _owner = owner.transform;
        _duration = duration;
        _shotsCount = shotsCount;
        _damagePerShot = damagePerShot;
        _radius = radius;
        _silenceDur = silenceDur;
        _applyDamage = applyDamage;
        NetworkGameplayResolver.TryResolveAttackerFromPlayer(owner, out _attackerClientId, out _attackerHealth);

        StartCoroutine(DealDamageRoutine());
    }

    private IEnumerator DealDamageRoutine()
    {
        float interval = _duration / _shotsCount;

        for (int i = 0; i < _shotsCount; i++)
        {
            ApplyDamagePulse();
            yield return new WaitForSeconds(interval);
        }

        Destroy(gameObject);
    }

    private void ApplyDamagePulse()
    {
        if (!_applyDamage)
            return;

        Collider[] hits = Physics.OverlapSphere(transform.position, _radius);

        foreach (Collider hit in hits)
        {
            if (!hit.CompareTag("Enemy"))
                continue;

            EnemyHealthSystem enemyHealth = hit.GetComponent<EnemyHealthSystem>();
            if (enemyHealth != null)
                enemyHealth.ApplyAuthoritativeDamage(_damagePerShot, 0f, false, _attackerClientId, _attackerHealth);
        }
    }

    void Update()
    {
        if (_owner != null) transform.position = _owner.position;
        transform.Rotate(Vector3.up * 720f * Time.deltaTime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, _radius > 0 ? _radius : 1f);
    }
}
