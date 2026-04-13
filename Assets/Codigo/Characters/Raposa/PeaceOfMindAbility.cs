using UnityEngine;

/// <summary>
/// ── PeaceOfMindAbility ───────────────────────────────────
/// ScriptableObject that activates the gradual healing ability.
///
///  ▸ Requires PeaceOfMindLogic pre-attached to the player prefab
///  ▸ Actual heal runs server-side via RequestPeaceOfMindServerRpc
///  ▸ AddComponent at runtime is prohibited on spawned NetworkObjects
/// ─────────────────────────────────────────────────────────
/// </summary>
[CreateAssetMenu(fileName = "Paz de Espirito", menuName = "ExoBeasts/Personagens/Raposa/Habilidade/Paz de Espirito")]
public class PeaceOfMindAbility : Ability
{
    [Header("Ingredientes da Cura")]
    public float totalHeal = 80f;
    public float duration = 3f;

    public override bool Activate(GameObject quemUsou)
    {
        // PeaceOfMindLogic must be pre-attached to the player prefab — AddComponent is forbidden on spawned NetworkObjects
        PeaceOfMindLogic ajudante = quemUsou.GetComponent<PeaceOfMindLogic>();
        if (ajudante == null)
        {
            Debug.LogError("PeaceOfMindAbility: PeaceOfMindLogic not found on player prefab. Add it in the editor.");
            return false;
        }

        ajudante.enabled = true;
        ajudante.StartEffect(totalHeal, duration, this);
        return true;
    }
}
