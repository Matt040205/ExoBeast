using UnityEngine;

/// <summary>
/// ── TrilhaSobrevivenciaCacadora ──────────────────────────
/// Upgrade path data: adds lifesteal (vampirism) to the character's attacks.
///
///  ▸ Pure data class — vampirism logic implemented in the attack system
///  ▸ No network logic needed; heal is applied via PlayerHealthSystem on server
/// ─────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Trilha Sobrevivencia da Cacadora", menuName = "ExoBeasts/Personagens/Coruja/Trilha/Sobrevivencia da Cacadora")]
public class TrilhaSobrevivenciaCacadora : UpgradePath
{
    [Header("Configuracoes da Trilha")]
    [Tooltip("Lifesteal percentage (0.1 = 10%).")]
    [Range(0, 1)]
    public float vampirismPercent = 0.1f;
    // Vampirism logic implemented in the character's attack system
}
