using System.Collections.Generic;
using UnityEngine;

namespace ExoBeasts.Multiplayer.Sync
{
    [RequireComponent(typeof(Rigidbody))]
    [RequireComponent(typeof(Collider))]
    public class ServerAuthoritativeProjectile : MonoBehaviour
    {
        private Rigidbody rb;
        private bool hasHit;

        private PlayerShooting ownerShooting;
        private ulong attackerClientId;
        private float damage;
        private bool isCritical;
        private bool isSilverBullet;
        private float armorPenetration;
        private bool isEmpoweredSkill;
        private float explosionRadius;
        private Vector3 sourcePosition;

        // Grace period: mesma proteção do ProjectileVisual.
        private float spawnTime;
        private const float SPAWN_GRACE_PERIOD = 0.05f;
        private Transform ownerRoot;

        private void Awake()
        {
            rb = GetComponent<Rigidbody>();
            rb.useGravity = false;
            rb.isKinematic = false;

            Collider projectileCollider = GetComponent<Collider>();
            if (projectileCollider != null)
                projectileCollider.isTrigger = true;
        }

        public void Initialize(
            PlayerShooting shooting,
            ulong attackerId,
            float projectileDamage,
            bool projectileCrit,
            bool projectileSilverBullet,
            float projectileArmorPenetration,
            Vector3 direction,
            float speed,
            float maxLifetime,
            bool empoweredSkill,
            float empoweredExplosionRadius)
        {
            ownerShooting = shooting;
            attackerClientId = attackerId;
            damage = projectileDamage;
            isCritical = projectileCrit;
            isSilverBullet = projectileSilverBullet;
            armorPenetration = projectileArmorPenetration;
            isEmpoweredSkill = empoweredSkill;
            explosionRadius = empoweredExplosionRadius;
            hasHit = false;
            spawnTime = Time.time;
            ownerRoot = shooting != null ? shooting.transform : null;
            sourcePosition = ownerRoot != null ? ownerRoot.position : transform.position;

            SuppressPresentation();

            rb.linearVelocity = direction.normalized * speed;
            CancelInvoke(nameof(DestroySelf));
            Invoke(nameof(DestroySelf), maxLifetime);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (hasHit)
                return;

            if (other == null || other.CompareTag("Player"))
                return;

            // Grace period: ignora colisões nos primeiros frames (muzzle flash, VFX).
            if (Time.time - spawnTime < SPAWN_GRACE_PERIOD)
                return;

            // Ignora colliders filhos do atirador.
            if (ownerRoot != null && other.transform.IsChildOf(ownerRoot))
                return;

            hasHit = true;
            rb.linearVelocity = Vector3.zero;

            float totalConfirmedDamage = 0f;

            if (isEmpoweredSkill && explosionRadius > 0f)
            {
                totalConfirmedDamage = ApplyExplosionDamage();
                ownerShooting?.BroadcastExplosionVfxFromServer(transform.position, explosionRadius);
            }
            else
            {
                totalConfirmedDamage = ApplyDamageToCollider(other);
            }

            if (totalConfirmedDamage > 0f)
                ownerShooting?.NotifyConfirmedDamageServer(totalConfirmedDamage);

            DestroySelf();
        }

        private float ApplyExplosionDamage()
        {
            float totalDamage = 0f;
            Collider[] overlaps = Physics.OverlapSphere(transform.position, explosionRadius);
            HashSet<NetworkedEnemy> processedEnemies = new HashSet<NetworkedEnemy>();

            foreach (Collider overlap in overlaps)
            {
                NetworkedEnemy enemy = overlap.GetComponentInParent<NetworkedEnemy>();
                if (enemy == null || !processedEnemies.Add(enemy))
                    continue;

                if (enemy.ApplyDamageServer(damage, armorPenetration, isCritical, attackerClientId, out float confirmedDamage, isSilverBullet, isAreaDamage: true, sourcePosition: transform.position))
                    totalDamage += confirmedDamage;
            }

            return totalDamage;
        }

        private float ApplyDamageToCollider(Collider other)
        {
            NetworkedEnemy enemy = other.GetComponentInParent<NetworkedEnemy>();
            if (enemy == null)
                return 0f;

            return enemy.ApplyDamageServer(damage, armorPenetration, isCritical, attackerClientId, out float confirmedDamage, isSilverBullet, isAreaDamage: false, sourcePosition: sourcePosition)
                ? confirmedDamage
                : 0f;
        }

        private void SuppressPresentation()
        {
            ProjectileVisual pooledVisual = GetComponent<ProjectileVisual>();
            if (pooledVisual != null)
                pooledVisual.enabled = false;

            foreach (Renderer renderer in GetComponentsInChildren<Renderer>(true))
                renderer.enabled = false;

            foreach (TrailRenderer trail in GetComponentsInChildren<TrailRenderer>(true))
                trail.enabled = false;

            foreach (ParticleSystem particleSystem in GetComponentsInChildren<ParticleSystem>(true))
                particleSystem.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            foreach (AudioSource audioSource in GetComponentsInChildren<AudioSource>(true))
                audioSource.enabled = false;
        }

        private void DestroySelf()
        {
            CancelInvoke();
            if (rb != null)
                rb.linearVelocity = Vector3.zero;

            Destroy(gameObject);
        }
    }
}
