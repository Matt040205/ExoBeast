using FMODUnity;
using Unity.Netcode;
using UnityEngine;

[CreateAssetMenu(fileName = "Temor Sismico", menuName = "ExoBeasts/Personagens/Dragao/Habilidade/Temor Sismico")]
public class HabilidadeTemorSismico : Ability
{
    [Header("Area de Impacto")]
    public float range = 15f;
    [Range(0, 360)] public float angle = 90f;
    public float damage = 30f;

    [Header("Controle de Grupo")]
    public float stunDuration = 2f;
    public float knockUpDuration = 2f;
    public float knockUpForce = 12f;

    [Header("Debuff")]
    [Tooltip("Multiplicador de dano recebido. 1.5 = 50% a mais.")]
    public float vulnerabilityMultiplier = 1.8f;
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
            Debug.LogError("[TemorSismico] Prefab nao configurado na habilidade.");
            return false;
        }

        CommanderAbilityController controller = quemUsou.GetComponent<CommanderAbilityController>();
        if (controller != null)
            controller.SetAbilityUsage(this, true);

        if (!string.IsNullOrEmpty(sfxSlam))
            RuntimeManager.PlayOneShot(sfxSlam, quemUsou.transform.position);

        TemorSismicoLogic logic = Object.Instantiate(
            logicPrefab,
            quemUsou.transform.position,
            AbilityAimUtility.ResolveAimRotation(quemUsou));

        logic.Setup(
            quemUsou,
            range,
            angle,
            damage,
            stunDuration,
            knockUpDuration,
            knockUpForce,
            vulnerabilityMultiplier,
            vulnerabilityDuration);

        bool isNetworkSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkSession && logic.TryGetComponent(out NetworkObject netObj))
            netObj.Spawn();

        return true;
    }
}
