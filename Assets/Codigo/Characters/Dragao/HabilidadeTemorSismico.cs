using UnityEngine;
using FMODUnity;
using Unity.Netcode;

[CreateAssetMenu(fileName = "Temor Sismico", menuName = "ExoBeasts/Personagens/Dragao/Habilidade/Temor Sismico")]
public class HabilidadeTemorSismico : Ability
{
    [Header("Configuracoes de Combate")]
    public float range = 15f;
    [Range(0, 360)] public float angle = 45f;
    public float damage = 100f;

    [Header("Controle de Grupo")]
    public float knockUpDuration = 2f;
    public float knockUpForce = 12f;

    [Header("Debuff (Vulnerabilidade)")]
    [Tooltip("Multiplicador de dano recebido. 1.5 = 50% a mais.")]
    public float vulnerabilityMultiplier = 1.5f;
    public float vulnerabilityDuration = 5f;

    [Header("Visual e Logica")]
    public TemorSismicoLogic logicPrefab;

    [Header("FMOD")]
    [EventRef]
    public string sfxSlam = "event:/SFX/SeismicSlam";

    public override bool Activate(GameObject quemUsou)
    {
        if (logicPrefab == null)
        {
            Debug.LogError("[TemorSismico] Prefab nao configurado na Habilidade!");
            return false;
        }

        CommanderAbilityController controller = quemUsou.GetComponent<CommanderAbilityController>();
        if (controller != null)
            controller.SetAbilityUsage(this, true);

        if (!string.IsNullOrEmpty(sfxSlam))
            RuntimeManager.PlayOneShot(sfxSlam, quemUsou.transform.position);

        // Instancia, configura e spawna em rede â€” todos os clientes veem o VFX.
        TemorSismicoLogic logic = Object.Instantiate(
            logicPrefab,
            quemUsou.transform.position,
            AbilityAimUtility.ResolveAimRotation(quemUsou));

        logic.Setup(quemUsou, range, angle, damage, knockUpDuration, knockUpForce,
            vulnerabilityMultiplier, vulnerabilityDuration);

        if (logic.TryGetComponent<NetworkObject>(out var netObj))
        {
            netObj.Spawn();
        }
        else
        {
            Debug.LogWarning("[TemorSismico] Prefab sem NetworkObject â€” VFX visivel apenas no servidor. Adicione NetworkObject ao prefab.");
        }

        return true;
    }
}
