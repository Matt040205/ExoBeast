using System;

public static class ObjectiveHealthBus
{
    public static event Action<float, float> OnObjectiveHealthChanged;

    private static float lastCurrentHealth;
    private static float lastMaxHealth;
    private static bool hasState;

    public static void Publish(float currentHealth, float maxHealth)
    {
        lastCurrentHealth = currentHealth;
        lastMaxHealth = maxHealth;
        hasState = true;
        OnObjectiveHealthChanged?.Invoke(currentHealth, maxHealth);
    }

    public static bool TryGetLastKnown(out float currentHealth, out float maxHealth)
    {
        currentHealth = lastCurrentHealth;
        maxHealth = lastMaxHealth;
        return hasState;
    }
}
