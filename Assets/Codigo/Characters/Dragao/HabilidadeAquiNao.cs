using UnityEngine;
using FMODUnity;

[CreateAssetMenu(fileName = "Aqui Nao", menuName = "ExoBeasts/Personagens/Dragao/Habilidade/Aqui Nao")]
public class HabilidadeAquiNao : Ability
{
    [Header("Configurações")]
    public float radius = 5f;
    public float damage = 40f;
    public float knockbackForce = 15f;
    public float stunDuration = 1f;

    [Header("Lógica")]
    public AquiNaoLogic logicPrefab;

    [Header("FMOD")]
    [EventRef]
    public string sfxSwing = "event:/SFX/HammerSwing";

    public override bool Activate(GameObject quemUsou)
    {
        if (logicPrefab == null) return true;

        CommanderAbilityController controller = quemUsou.GetComponent<CommanderAbilityController>();

        // Toca som
        if (!string.IsNullOrEmpty(sfxSwing))
        {
            RuntimeManager.PlayOneShot(sfxSwing, quemUsou.transform.position);
        }

        AquiNaoLogic logic = Instantiate(logicPrefab, quemUsou.transform.position, quemUsou.transform.rotation);
        logic.Setup(quemUsou, radius, damage, knockbackForce, stunDuration, controller, this);

        return true;
    }
}