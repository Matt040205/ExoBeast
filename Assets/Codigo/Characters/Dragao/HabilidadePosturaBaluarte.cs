using UnityEngine;

[CreateAssetMenu(fileName = "Postura de Baluarte", menuName = "ExoBeasts/Personagens/Dragao/Habilidade/Postura de Baluarte")]
public class HabilidadePosturaBaluarte : Ability
{
    public float duration = 4f;
    public float tauntRadius = 10f;
    public float tauntTickInterval = 0.2f;

    [Header("Legado")]
    public float counterDamage = 50f;
    public float counterKnockback = 10f;

    [Header("Visual")]
    public GameObject shieldVfxPrefab;

    public PosturaBaluarteLogic logicPrefab;

    public override bool Activate(GameObject quemUsou)
    {
        if (quemUsou == null)
            return false;

        DragonDefensiveStanceController defensiveStance = quemUsou.GetComponent<DragonDefensiveStanceController>();
        if (defensiveStance == null)
        {
            Debug.LogError("[HabilidadePosturaBaluarte] DragonDefensiveStanceController ausente no prefab do Dragao.");
            return false;
        }

        CommanderAbilityController controller = quemUsou.GetComponent<CommanderAbilityController>();
        return defensiveStance.ActivateServer(duration, tauntRadius, tauntTickInterval, controller, this);
    }
}
