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

        // Lógica de counter roda no servidor
        PosturaBaluarteLogic logic = Instantiate(logicPrefab, quemUsou.transform);
        logic.Setup(quemUsou, duration, counterDamage, counterKnockback, controller, this);

        // Proxy para o owner-cliente receber feedback visual e isCountering local
        controller?.StartLocalPosturaBaluarteOwnerProxy(duration);

        return true;
    }
}