using UnityEngine;

[CreateAssetMenu(fileName = "Postura de Baluarte", menuName = "ExoBeasts/Personagens/Dragao/Habilidade/Postura de Baluarte")]
public class HabilidadePosturaBaluarte : Ability
{
    public float duration = 4f;
    public float counterDamage = 50f;
    public float counterKnockback = 10f;

    public PosturaBaluarteLogic logicPrefab;

    public override bool Activate(GameObject quemUsou)
    {
        if (logicPrefab == null) return true;

        CommanderAbilityController controller = quemUsou.GetComponent<CommanderAbilityController>();

        // Instancia como FILHO do player para seguir ele
        PosturaBaluarteLogic logic = Instantiate(logicPrefab, quemUsou.transform);
        logic.Setup(quemUsou, duration, counterDamage, counterKnockback, controller, this);

        return true;
    }
}