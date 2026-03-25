using UnityEngine;

[CreateAssetMenu(fileName = "Mergulho na Tinta", menuName = "ExoBeasts/Personagens/Polvo/Habilidade/Mergulho na Tinta")]
public class HabilidadeMergulhoTinta : Ability
{
    [Header("Configurações do Mergulho")]
    public float duration = 3f;
    public float exitDamage = 40f;
    public float damageRadius = 4f;

    [Tooltip("O prefab visual da poça (sem collider!)")]
    public GameObject visualPuddlePrefab;

    public override bool Activate(GameObject quemUsou)
    {
        if (quemUsou.GetComponent<MergulhoTintaLogic>() != null) return false;

        MergulhoTintaLogic logic = quemUsou.AddComponent<MergulhoTintaLogic>();
        logic.StartDive(duration, exitDamage, damageRadius, visualPuddlePrefab, this);

        return true;
    }
}