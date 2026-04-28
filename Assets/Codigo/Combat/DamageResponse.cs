public struct DamageResponse
{
    public bool WasBlocked;
    public float ModifiedDamage;

    public static DamageResponse PassThrough(float damage)
    {
        return new DamageResponse
        {
            WasBlocked = false,
            ModifiedDamage = damage
        };
    }
}
