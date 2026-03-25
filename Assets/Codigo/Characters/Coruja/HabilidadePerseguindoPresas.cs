using UnityEngine;
using FMODUnity;
using Unity.Netcode;

/// <summary>
/// ── HabilidadePerseguindoPresas ──────────────────────────
/// ScriptableObject that spawns the PreyMarkLogic on the server to mark all active enemies.
///
///  ▸ Server-only spawn gate prevents duplicate logic objects
///  ▸ FMOD activation sound delegated to CommanderAbilityController feedback layer
///  ▸ PreyMarkLogic iterates SpawnedObjects directly — no FindGameObjectsWithTag
/// ─────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Perseguindo as Presas", menuName = "ExoBeasts/Personagens/Coruja/Habilidade/Perseguindo as Presas")]
public class HabilidadePerseguindoPresas : Ability
{
    [Header("Configuracoes da Habilidade")]
    public float markDuration = 5f;
    public float bonusDamageMultiplier = 1.25f;

    [Tooltip("Arraste o prefab da logica da habilidade aqui.")]
    public PreyMarkLogic logicPrefab;

    [Header("FMOD")]
    [EventRef]
    public string eventoTEC = "event:/SFX/TEC";

    public override bool Activate(GameObject quemUsou)
    {
        if (logicPrefab == null)
            return true;

        if (!NetworkManager.Singleton.IsServer) return true;

        // Activation sound handled by CommanderAbilityController's visual/audio feedback layer
        CommanderAbilityController abilityController = quemUsou.GetComponent<CommanderAbilityController>();

        PreyMarkLogic logic = Object.Instantiate(logicPrefab, quemUsou.transform.position, Quaternion.identity);
        logic.GetComponent<NetworkObject>().Spawn();
        logic.StartEffect(markDuration, bonusDamageMultiplier, abilityController, this);

        return true;
    }
}
