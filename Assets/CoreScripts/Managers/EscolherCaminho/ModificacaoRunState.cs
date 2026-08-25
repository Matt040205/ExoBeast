using UnityEngine;

public static class ModificacaoRunState
{
    private static ModificacaoData positivaAtiva;
    private static ModificacaoData negativaAtiva;

    public static ModificacaoData PositivaAtiva => positivaAtiva;
    public static ModificacaoData NegativaAtiva => negativaAtiva;

    public static void SetActive(ModificacaoData positiva, ModificacaoData negativa)
    {
        positivaAtiva = positiva;
        negativaAtiva = negativa;
    }

    public static void Clear()
    {
        positivaAtiva = null;
        negativaAtiva = null;
    }

    public static bool IsActive(ModificacaoGameplayEffect effect)
    {
        return Matches(positivaAtiva, effect) || Matches(negativaAtiva, effect);
    }

    public static float GetValue(ModificacaoGameplayEffect effect, float fallback = 0f)
    {
        if (Matches(positivaAtiva, effect))
            return positivaAtiva.valor;

        if (Matches(negativaAtiva, effect))
            return negativaAtiva.valor;

        return fallback;
    }

    public static float GetSecondaryValue(ModificacaoGameplayEffect effect, float fallback = 0f)
    {
        if (Matches(positivaAtiva, effect))
            return positivaAtiva.valorSecundario;

        if (Matches(negativaAtiva, effect))
            return negativaAtiva.valorSecundario;

        return fallback;
    }

    public static float Multiply(float value, ModificacaoGameplayEffect effect)
    {
        return IsActive(effect) ? value * GetValue(effect, 1f) : value;
    }

    public static int ApplyTowerPlacementCost(int baseCost)
    {
        if (IsActive(ModificacaoGameplayEffect.EconomiaHacker))
            return 0;

        return Mathf.Max(0, Mathf.RoundToInt(Multiply(baseCost, ModificacaoGameplayEffect.ImpostoTatico)));
    }

    public static int ApplyUpgradeCost(int baseCost)
    {
        if (IsActive(ModificacaoGameplayEffect.EconomiaHacker))
            return 0;

        return Mathf.Max(0, baseCost);
    }

    public static int ApplyGeoditeReward(int baseReward)
    {
        float reward = baseReward;
        reward = Multiply(reward, ModificacaoGameplayEffect.RecursosEscassos);
        reward = Multiply(reward, ModificacaoGameplayEffect.EconomiaPositiva);

        if (baseReward > 0)
            return Mathf.Max(1, Mathf.RoundToInt(reward));

        return Mathf.Max(0, Mathf.RoundToInt(reward));
    }

    public static float ApplyEnemyHealth(float health)
    {
        health = Multiply(health, ModificacaoGameplayEffect.InimigosReforcados);
        health = Multiply(health, ModificacaoGameplayEffect.Gigantismo);
        return Mathf.Max(1f, health);
    }

    public static float ApplyEnemyDamage(float damage)
    {
        damage = Multiply(damage, ModificacaoGameplayEffect.DanoResidual);
        damage = Multiply(damage, ModificacaoGameplayEffect.FrenesiMortal);
        return Mathf.Max(0f, damage);
    }

    public static float ApplyEnemyMoveSpeed(float speed)
    {
        speed = Multiply(speed, ModificacaoGameplayEffect.AgilidadeInimiga);
        speed = Multiply(speed, ModificacaoGameplayEffect.FrenesiMortal);

        if (IsActive(ModificacaoGameplayEffect.AlvosMinusculos))
            speed *= GetSecondaryValue(ModificacaoGameplayEffect.AlvosMinusculos, 1.5f);

        if (IsActive(ModificacaoGameplayEffect.Gigantismo))
            speed *= GetSecondaryValue(ModificacaoGameplayEffect.Gigantismo, 0.5f);

        return Mathf.Max(0.1f, speed);
    }

    public static float ApplyPlayerReloadTime(float reloadTime)
    {
        return Mathf.Max(0.01f, Multiply(reloadTime, ModificacaoGameplayEffect.MunicaoDefeituosa));
    }

    public static float ApplyAbilityCooldown(float cooldown)
    {
        if (IsActive(ModificacaoGameplayEffect.ModoOverclock))
            return 0f;

        return Mathf.Max(0f, Multiply(cooldown, ModificacaoGameplayEffect.FadigaCibernetica));
    }

    public static float ApplyPlayerCritChance(float critChance)
    {
        if (!IsActive(ModificacaoGameplayEffect.TirosCriticos))
            return critChance;

        return Mathf.Clamp01(critChance + GetValue(ModificacaoGameplayEffect.TirosCriticos, 0f));
    }

    public static float ApplyPlayerMoveSpeedMultiplier(float multiplier)
    {
        return Multiply(multiplier, ModificacaoGameplayEffect.MobilidadeAumentada);
    }

    public static float ApplyPlayerShotSpread(float spreadDegrees)
    {
        return Mathf.Max(0f, Multiply(spreadDegrees, ModificacaoGameplayEffect.MiraLaser));
    }

    public static bool RollsChance(ModificacaoGameplayEffect effect)
    {
        return IsActive(effect) && Random.value < Mathf.Clamp01(GetValue(effect, 0f));
    }

    private static bool Matches(ModificacaoData data, ModificacaoGameplayEffect effect)
    {
        return data != null && data.efeito == effect;
    }
}
