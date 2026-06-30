using UnityEngine;

/// <summary>
/// ── TrilhaFocoImplacavel ─────────────────────────────────
/// Upgrade path data: reduces ability cooldowns on headshot.
///
///  ▸ Pure data class — cooldown reduction implemented in the combat system
///  ▸ No network logic needed; cooldown state lives in CommanderAbilityController
/// ─────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Trilha Foco Implacavel", menuName = "ExoBeasts/Personagens/Coruja/Trilha/Foco Implacavel")]
public class TrilhaFocoImplacavel : UpgradePath
{
    [Header("Configuracoes da Trilha")]
    [Tooltip("Cooldown reduction on headshot as a percentage.")]
    [Range(0, 1)]
    public float cooldownReductionPercent = 0.3f;
    // Cooldown reduction logic implemented in the character's combat system
}
