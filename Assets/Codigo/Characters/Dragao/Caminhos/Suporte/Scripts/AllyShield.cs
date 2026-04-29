using UnityEngine;

public class AllyShield : MonoBehaviour
{
    public float CurrentShield { get; private set; }
    public bool IsActive => CurrentShield > 0;
    private bool explodeOnBreak = false;

    private System.Action<ulong> onShieldBrokenRPC;
    private ulong targetNetId;

    public void ApplyShield(float amount, TowerController source, bool canExplode, ulong netId = 0, System.Action<ulong> rpcCallback = null)
    {
        CurrentShield = amount;
        explodeOnBreak = canExplode;
        targetNetId = netId;
        onShieldBrokenRPC = rpcCallback;
    }

    public float AbsorbDamage(float damage)
    {
        if (CurrentShield >= damage)
        {
            CurrentShield -= damage;
            return 0f;
        }
        else
        {
            float remainder = damage - CurrentShield;
            CurrentShield = 0;
            // Shield quebrou!
            if (explodeOnBreak) TriggerBreakExplosion();
            
            if (onShieldBrokenRPC != null && targetNetId != 0)
                onShieldBrokenRPC.Invoke(targetNetId);

            return remainder;
        }
    }

    private void TriggerBreakExplosion()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, 4f);
        foreach (var hit in hits)
        {
            if (hit.CompareTag("Enemy"))
            {
                EnemyController enemy = hit.GetComponent<EnemyController>();
                if (enemy != null)
                {
                    Vector3 dir = (enemy.transform.position - transform.position).normalized;
                    enemy.ApplyKnockback(dir + Vector3.up * 0.5f, 15f); // Knockback forte
                }
            }
        }
    }
}
