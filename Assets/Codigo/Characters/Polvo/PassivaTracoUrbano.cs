using UnityEngine;

[CreateAssetMenu(fileName = "Traço Urbano", menuName = "ExoBeasts/Personagens/Polvo/Passiva/Traço Urbano")]
public class PassivaTracoUrbano : PassivaAbility
{
    [Header("Configurações do Traço")]
    public float speedBoostMultiplier = 1.3f; // 30% mais rápido
    public float inkDuration = 5f;
    public float slowPercent = 0.3f; // 30% de lentidão

    [Tooltip("Prefab da poça de tinta que fica no chão")]
    public GameObject inkTrailPrefab;

    public override void OnEquip(GameObject owner)
    {
        // Adiciona o componente lógico ao jogador
        TracoUrbanoLogic logic = owner.AddComponent<TracoUrbanoLogic>();
        logic.Initialize(speedBoostMultiplier, inkDuration, slowPercent, inkTrailPrefab);
    }

    public override void OnUnequip(GameObject owner)
    {
        TracoUrbanoLogic logic = owner.GetComponent<TracoUrbanoLogic>();
        if (logic != null) Destroy(logic);
    }
}