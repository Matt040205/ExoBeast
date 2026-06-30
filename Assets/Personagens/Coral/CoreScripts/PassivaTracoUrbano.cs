using UnityEngine;
using Unity.Netcode;

[CreateAssetMenu(fileName = "Traço Urbano", menuName = "ExoBeasts/Personagens/Polvo/Passiva/Traço Urbano")]
public class PassivaTracoUrbano : PassivaAbility
{
    [Header("Configurações do Traço")]
    public float speedBoostMultiplier = 1.3f;
    public float inkDuration = 5f;
    public float slowPercent = 0.3f;

    [Tooltip("Prefab da poça de tinta que fica no chão")]
    public GameObject inkTrailPrefab;

    public override void OnEquip(GameObject owner)
    {
        if (owner == null)
            return;

        NetworkObject networkObject = owner.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned && !networkObject.IsOwner)
            return;

        TracoUrbanoLogic logic = owner.GetComponent<TracoUrbanoLogic>();
        if (logic == null)
            logic = owner.AddComponent<TracoUrbanoLogic>();

        logic.Initialize(speedBoostMultiplier, inkDuration, slowPercent, inkTrailPrefab);
    }

    public override void OnUnequip(GameObject owner)
    {
        if (owner == null)
            return;

        NetworkObject networkObject = owner.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned && !networkObject.IsOwner)
            return;

        TracoUrbanoLogic logic = owner.GetComponent<TracoUrbanoLogic>();
        if (logic != null)
            Object.Destroy(logic);
    }
}
