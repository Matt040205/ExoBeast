using UnityEngine;

public abstract class PassivaAbility : ScriptableObject
{
    public string abilityName;
    [TextArea] public string description;
    public Sprite icon;

    public abstract void OnEquip(GameObject owner);
    public abstract void OnUnequip(GameObject owner);
}