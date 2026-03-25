using UnityEngine;

/// <summary>
/// ── NineTailsDanceAbility ────────────────────────────────
/// ScriptableObject that activates the Nine Tails Dance ultimate.
///
///  ▸ Requires NineTailsDanceLogic pre-attached to the player prefab
///  ▸ Cooldown reduction applied before the state change via ServerRpc
///  ▸ AddComponent at runtime is prohibited on spawned NetworkObjects
/// ─────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Danca das Nove Caudas", menuName = "ExoBeasts/Personagens/Raposa/Habilidade/Danca das Nove Caudas")]
public class NineTailsDanceAbility : Ability
{
    [Header("Ingredientes da Ultimate")]
    public float duration = 8f;
    [Range(0, 1)]
    public float cooldownReductionPercent = 0.4f;

    public override bool Activate(GameObject quemUsou)
    {
        CommanderAbilityController controller = quemUsou.GetComponent<CommanderAbilityController>();
        if (controller == null) return true;

        controller.ReduceAllAbilityCooldowns(cooldownReductionPercent);

        // NineTailsDanceLogic must be pre-attached to the player prefab — AddComponent is forbidden on spawned NetworkObjects
        NineTailsDanceLogic ajudante = quemUsou.GetComponent<NineTailsDanceLogic>();
        if (ajudante == null)
        {
            Debug.LogError("NineTailsDanceAbility: NineTailsDanceLogic not found on player prefab. Add it in the editor.");
            return true;
        }

        ajudante.enabled = true;
        ajudante.StartEffect(duration);
        return true;
    }
}
