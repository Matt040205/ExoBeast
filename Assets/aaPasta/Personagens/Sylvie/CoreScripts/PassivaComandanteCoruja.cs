using UnityEngine;

/// <summary>
/// ── PassivaComandanteCoruja ──────────────────────────────
/// Passive: boosts all tower damage and enables double jump for the owl.
///
///  ▸ ScriptableObject — activated once via CommanderAbilityController.OnEquip
///  ▸ Tower damage bonus via BuildManager (commented until API is finalized)
///  ▸ Double jump toggled directly on PlayerMovement (owner-local, no network needed)
/// ─────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Passiva de Comandante da Coruja", menuName = "ExoBeasts/Personagens/Coruja/Passiva/Passiva de Comandante")]
public class PassivaComandanteCoruja : PassivaAbility
{
    [Header("Configuracoes da Passiva")]
    [Tooltip("Tower damage boost as a percentage.")]
    [Range(0, 1)]
    public float bonusDamagePercent = 0.2f;
    [Tooltip("Grants double-jump to the character.")]
    public bool canDoubleJump = true;

    public BuildManager buildManager;

    public override void OnEquip(GameObject owner)
    {
        // buildManager.ApplyDamageBonusToAllTowers(bonusDamagePercent); — pending BuildManager API

        PlayerMovement playerMovement = owner.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.canDoubleJump = canDoubleJump;
        }
    }

    public override void OnUnequip(GameObject owner)
    {
        // buildManager.RemoveDamageBonusFromAllTowers(bonusDamagePercent); — pending BuildManager API

        PlayerMovement playerMovement = owner.GetComponent<PlayerMovement>();
        if (playerMovement != null)
        {
            playerMovement.canDoubleJump = false;
        }
    }
}
