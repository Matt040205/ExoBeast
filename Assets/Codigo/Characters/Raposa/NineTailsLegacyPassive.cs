using UnityEngine;

/// <summary>
/// ── NineTailsLegacyPassive ───────────────────────────────
/// Passive ability: grants a permanent attack-speed bonus to the fox.
///
///  ▸ ScriptableObject — activated once by CommanderAbilityController.OnEquip
///  ▸ No network logic needed: attack speed is read server-side via CharacterData
///  ▸ OnUnequip notifies via Debug to aid editor testing
/// ─────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Legado das Nove Caudas", menuName = "ExoBeasts/Personagens/Raposa/Passiva/Legado das Nove Caudas")]
public class NineTailsLegacyPassive : PassivaAbility
{
    [Header("Ingredientes da Passiva")]
    public float attackSpeedBonus = 0.15f;

    public override void OnEquip(GameObject owner)
    {
    }

    public override void OnUnequip(GameObject owner)
    {
        Debug.Log("Passiva desequipada: " + abilityName);
    }
}
