using UnityEngine;
using System.Collections.Generic;

public enum StatType { Damage, AttackSpeed, Range, Armor, CritChance, CritDamage, ArmorPenetration }
public enum ModificationType { Additive, Multiplicative }

[System.Serializable]
public struct StatModifier
{
    public StatType statToModify;
    public ModificationType modType;
    public float value;
}

[CreateAssetMenu(fileName = "Novo Upgrade", menuName = "ScriptableObjects/Trilhas/Nivel")]
public class Upgrade : ScriptableObject
{
    [Header("Informacoes do Upgrade")]
    public string upgradeName;
    [TextArea] public string description;

    [Header("Custos")]
    public int geoditeCost;
    public int darkEtherCost;

    [Header("Bonus de Status Simples")]
    public List<StatModifier> modifiers;

    [Header("Comportamento Especial Desbloqueado")]
    [Tooltip("Arraste o PREFAB (GameObject) do comportamento que este upgrade desbloqueia.")]
    public GameObject behaviorToUnlock;
}
