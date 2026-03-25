using UnityEngine;
using FMODUnity;

[CreateAssetMenu(fileName = "Temor Sismico", menuName = "ExoBeasts/Personagens/Dragao/Habilidade/Temor Sismico")]
public class HabilidadeTemorSismico : Ability
{
    [Header("Configurações de Combate")]
    public float range = 15f;
    [Range(0, 360)] public float angle = 45f; // Ângulo do cone
    public float damage = 100f;

    [Header("Controle de Grupo")]
    public float knockUpDuration = 2f;
    public float knockUpForce = 12f;

    [Header("Debuff (Vulnerabilidade)")]
    [Tooltip("Multiplicador de dano recebido. 1.5 = 50% a mais.")]
    public float vulnerabilityMultiplier = 1.5f;
    public float vulnerabilityDuration = 5f;

    [Header("Visual e Lógica")]
    public TemorSismicoLogic logicPrefab;

    [Header("FMOD")]
    [EventRef]
    public string sfxSlam = "event:/SFX/SeismicSlam";

    public override bool Activate(GameObject quemUsou)
    {
        if (logicPrefab == null)
        {
            Debug.LogError("Prefab do Temor Sísmico não configurado na Habilidade!");
            return false;
        }

        // 1. Gerencia o Cooldown e Uso na UI (Lógica do Player)
        CommanderAbilityController controller = quemUsou.GetComponent<CommanderAbilityController>();
        if (controller != null)
        {
            controller.SetAbilityUsage(this, true);
        }

        // 2. Toca o Som
        if (!string.IsNullOrEmpty(sfxSlam))
        {
            RuntimeManager.PlayOneShot(sfxSlam, quemUsou.transform.position);
        }

        // 3. Instancia o efeito visual/lógico
        TemorSismicoLogic logic = Instantiate(logicPrefab, quemUsou.transform.position, quemUsou.transform.rotation);

        // 4. Passa os dados (INCLUINDO VULNERABILIDADE) para a lógica executar
        logic.Setup(quemUsou, range, angle, damage, knockUpDuration, knockUpForce, vulnerabilityMultiplier, vulnerabilityDuration);

        return true;
    }
}