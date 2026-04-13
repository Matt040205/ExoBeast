using UnityEngine;

public class PosturaBaluarteLogic : MonoBehaviour
{
    private float counterDamage;
    private float counterForce;
    private GameObject owner;
    private PlayerHealthSystem playerHealth;

    public void Setup(GameObject owner, float duration, float dmg, float force, CommanderAbilityController controller, Ability ability)
    {
        this.owner = owner;
        this.counterDamage = dmg;
        this.counterForce = force;

        if (controller != null) controller.SetAbilityUsage(ability, true);

        playerHealth = owner.GetComponent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.isCountering = true;
        }

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

    void OnDestroy()
    {
        if (playerHealth != null)
        {
            playerHealth.isCountering = false;
        }
    }

    void OnTriggerEnter(Collider other)
    {
        if (other.CompareTag("Enemy"))
        {
            Vector3 dirToEnemy = (other.transform.position - owner.transform.position).normalized;
            float dot = Vector3.Dot(owner.transform.forward, dirToEnemy);

            if (dot > 0.5f)
            {
                PerformCounterAttack(other.gameObject);
            }
        }
    }

    void PerformCounterAttack(GameObject enemy)
    {
        EnemyHealthSystem hp = enemy.GetComponent<EnemyHealthSystem>();
        if (hp != null) hp.TakeDamage(counterDamage);

        EnemyController ai = enemy.GetComponent<EnemyController>();
        if (ai != null)
        {
            ai.ApplySlip();
            ai.ApplyKnockback(owner.transform.forward, counterForce);
        }
    }
}