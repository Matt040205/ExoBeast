using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── HabilidadeVooGracioso ────────────────────────────────
/// ScriptableObject that spawns the VooGraciosoLogic on the server.
///
///  ▸ Cannot activate while grounded — ability is airborne-only
///  ▸ Spawn parented to player so the logic moves with the character
///  ▸ Server calls StartEffect after Spawn to set NetworkVariables before clients receive the object
/// ─────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Voo Gracioso", menuName = "ExoBeasts/Personagens/Coruja/Habilidade/Voo Gracioso")]
public class HabilidadeVooGracioso : Ability
{
    [Header("Configuracoes da Habilidade")]
    public float jumpHeightModifier = 1.5f;
    public float staticAimDuration = 3f;
    public float bonusDamageMultiplier = 1.5f;
    public float bonusExplosionRadius = 5f;

    [Tooltip("Arraste o prefab da logica da habilidade aqui.")]
    public VooGraciosoLogic logicPrefab;

    public override bool Activate(GameObject quemUsou)
    {
        if (logicPrefab == null)
            return true;

        if (!NetworkManager.Singleton.IsServer) return true;

        PlayerMovement movement = quemUsou.GetComponent<PlayerMovement>();
        if (movement != null && movement.isGrounded)
        {
            return false; // Cannot activate while grounded
        }

        CommanderAbilityController abilityController = quemUsou.GetComponent<CommanderAbilityController>();

        VooGraciosoLogic logic = Object.Instantiate(logicPrefab, quemUsou.transform);
        logic.GetComponent<NetworkObject>().Spawn();
        logic.StartEffect(
            quemUsou,
            jumpHeightModifier,
            staticAimDuration,
            bonusDamageMultiplier,
            bonusExplosionRadius,
            abilityController,
            this
        );

        return true;
    }
}
