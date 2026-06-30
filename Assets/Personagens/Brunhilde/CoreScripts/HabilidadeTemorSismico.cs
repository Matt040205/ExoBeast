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
    
    [Header("Efeito Visual (Ground Slash)")]
    public GameObject groundSlashPrefab;
    public int numberOfSlashes = 3;
    public float travelSpeed = 14f;
    public float travelTime = 1.5f;
    public float slowDownRate = 0.5f;
    public float fadeOutGracePeriod = 2.0f;

    [Header("FMOD")]
    public string sfxSlam = AudioEventIds.SfxSeismicSlam;

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
            ExoAudioService.PlayOneShot3D(sfxSlam, quemUsou.transform.position);

        TemorSismicoLogic logic = Object.Instantiate(
            logicPrefab,
            quemUsou.transform.position,
            AbilityAimUtility.ResolveAimRotation(quemUsou));

        // ATENCAO BUG FIX (Bug 1 - 7 Maio 2026): groundSlashPrefab e demais params visuais agora vivem
        // no prefab TemorSismico.prefab (SerializedField em TemorSismicoLogic). Os parametros do SO
        // ainda sao passados como override (server-only) — mas para os clientes verem o visual,
        // o PREFAB precisa ter os mesmos valores no Inspector. Idealmente os campos visuais devem
        // ser removidos do SO no futuro para evitar duplicacao (single source of truth no prefab).
        logic.Setup(
            quemUsou,
            range,
            angle,
            damage,
            stunDuration,
            knockUpDuration,
            knockUpForce,
            vulnerabilityMultiplier,
            vulnerabilityDuration,
            groundSlashPrefab,
            numberOfSlashes,
            travelSpeed,
            travelTime,
            slowDownRate,
            fadeOutGracePeriod);

        bool isNetworkSession = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        if (isNetworkSession && logic.TryGetComponent(out NetworkObject netObj))
            netObj.Spawn();

        return true;
    }
}
