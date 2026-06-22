using UnityEngine;
using ExoBeasts.Multiplayer.Sync;

public class PosturaBaluarteLogic : MonoBehaviour
{
    private float counterDamage;
    private float counterForce;
    private GameObject owner;
    private Transform facingTransform;
    private PlayerHealthSystem playerHealth;
    private ulong attackerClientId;
    private PlayerHealthSystem attackerHealth;

    public void Setup(GameObject owner, float duration, float dmg, float force, CommanderAbilityController controller, Ability ability)
    {
        this.owner = owner;
        counterDamage = dmg;
        counterForce = force;
        facingTransform = AbilityAimUtility.ResolveAimTransform(owner);
        NetworkGameplayResolver.TryResolveAttackerFromPlayer(owner, out attackerClientId, out attackerHealth);

        if (controller != null)
            controller.SetAbilityUsage(ability, true);

        playerHealth = owner.GetComponent<PlayerHealthSystem>();
        if (playerHealth != null)
            playerHealth.isCountering = true;

        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb == null)
        {
            rb = gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        SphereCollider col = GetComponent<SphereCollider>();
        if (col == null)
        {
            col = gameObject.AddComponent<SphereCollider>();
            col.isTrigger = true;
            col.radius = 2f;
        }

        Destroy(gameObject, duration);
    }

    public void SetupProxy(float duration)
    {
        playerHealth = GetComponentInParent<PlayerHealthSystem>();
        if (playerHealth != null) playerHealth.isCountering = true;

        SphereCollider col = GetComponent<SphereCollider>();
        if (col != null) col.enabled = false;

        Destroy(gameObject, duration);
    }

    void OnDestroy()
    {
        if (playerHealth != null)
            playerHealth.isCountering = false;
    }

    void OnTriggerEnter(Collider other)
    {
        if (!other.CompareTag("Enemy"))
            return;

        Vector3 dirToEnemy = (other.transform.position - owner.transform.position).normalized;
        float dot = Vector3.Dot(GetOwnerForward(), dirToEnemy);

        if (dot > 0.5f)
            PerformCounterAttack(other.gameObject);
    }

    void PerformCounterAttack(GameObject enemy)
    {
        EnemyHealthSystem hp = enemy.GetComponent<EnemyHealthSystem>();
        if (hp != null)
            hp.ApplyAuthoritativeDamage(counterDamage, 0f, false, attackerClientId, attackerHealth);

        EnemyController ai = enemy.GetComponent<EnemyController>();
        if (ai != null)
        {
            ai.ApplySlip();
            ai.ApplyKnockback(GetOwnerForward(), counterForce);
        }
    }

    private Vector3 GetOwnerForward()
    {
        if (facingTransform == null && owner != null)
            facingTransform = AbilityAimUtility.ResolveAimTransform(owner);

        Transform source = facingTransform != null ? facingTransform : owner != null ? owner.transform : transform;
        return AbilityAimUtility.ResolveFlatForward(source);
    }
}
