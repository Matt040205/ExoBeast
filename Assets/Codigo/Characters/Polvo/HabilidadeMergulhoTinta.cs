using UnityEngine;

[CreateAssetMenu(fileName = "Mergulho na Tinta", menuName = "ExoBeasts/Personagens/Polvo/Habilidade/Mergulho na Tinta")]
public class HabilidadeMergulhoTinta : Ability
{
    [Header("Configuracoes do Mergulho")]
    public float duration = 3f;
    public float exitDamage = 40f;
    public float damageRadius = 4f;

    [Tooltip("O prefab visual da poca (sem collider!)")]
    public GameObject visualPuddlePrefab;

    [Header("Shader de Dissolve (opcional)")]
    [Tooltip("Material com _dissolveamount (0=visivel, 1=invisivel). Null = desabilita renderers imediatamente.")]
    public Material diveShaderMaterial;
    [Range(0.1f, 2f)]
    public float dissolveDuration = 0.4f;

    public override bool Activate(GameObject quemUsou)
    {
        if (quemUsou.GetComponent<MergulhoTintaLogic>() != null)
            return false;

        MergulhoTintaLogic logic = quemUsou.AddComponent<MergulhoTintaLogic>();
        if (!logic.StartDive(
                duration,
                exitDamage,
                damageRadius,
                visualPuddlePrefab,
                this))
        {
            return false;
        }

        CommanderAbilityController controller = quemUsou.GetComponent<CommanderAbilityController>();
        controller?.StartLocalMergulhoTintaOwnerProxy(
            duration,
            exitDamage,
            damageRadius);

        return true;
    }
}
