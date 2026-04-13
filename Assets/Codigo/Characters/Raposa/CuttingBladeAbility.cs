using UnityEngine;
using FMODUnity;

/// <summary>
/// ── CuttingBladeAbility ──────────────────────────────────
/// ScriptableObject that triggers the fox's dash-with-damage ability.
///
///  ▸ Requires CuttingBladeLogic pre-attached to the player prefab
///  ▸ Server validation and damage done inside CuttingBladeLogic.PerformDashDamageServerRpc
///  ▸ AddComponent at runtime is prohibited on spawned NetworkObjects
/// ─────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Lamina Cortante", menuName = "ExoBeasts/Personagens/Raposa/Habilidade/Lamina Cortante")]
public class CuttingBladeAbility : Ability
{
    [Header("Ingredientes da Lamina")]
    public float dashDistance = 7f;
    public float damage = 60f;
    public bool resetCooldownOnKill = true;

    [Header("FMOD")]
    [EventRef]
    public string eventoDash = "event:/SFX/Dash";

    public override void Initialize()
    {
    }

    public override bool Activate(GameObject quemUsou)
    {
        CharacterController controller = quemUsou.GetComponent<CharacterController>();
        PlayerMovement movementScript = quemUsou.GetComponent<PlayerMovement>();
        CommanderAbilityController abilityController = quemUsou.GetComponent<CommanderAbilityController>();

        if (controller == null || movementScript == null || abilityController == null)
        {
            Debug.LogError("CuttingBladeAbility: Missing components (Controller, PlayerMovement or AbilityController).");
            return false;
        }

        Transform modelPivot = movementScript.GetModelPivot();
        if (modelPivot == null) return false;

        // CuttingBladeLogic must be pre-attached to the player prefab — AddComponent is forbidden on spawned NetworkObjects
        CuttingBladeLogic logic = quemUsou.GetComponent<CuttingBladeLogic>();
        if (logic == null)
        {
            Debug.LogError("CuttingBladeAbility: CuttingBladeLogic not found on player prefab. Add it in the editor.");
            return false;
        }

        logic.enabled = true;
        logic.StartDash(
            quemUsou,
            controller,
            modelPivot,
            dashDistance,
            damage,
            eventoDash,
            abilityController,
            this,
            resetCooldownOnKill
        );

        return true;
    }
}
