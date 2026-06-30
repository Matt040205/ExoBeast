using UnityEngine;
using System.Collections.Generic;

// Adicionei "Special" ao final da lista
public enum CharacterStatType
{
    MaxHealth,
    Damage,
    MoveSpeed,
    AttackSpeed,
    Range,
    Armor,
    CritChance,
    CritDamage,
    ArmorPenetration,
    Special // Novo tipo para lógica personalizada
}

[System.Serializable]
public struct CharacterStatModifier
{
    public CharacterStatType statToModify;
    public ModificationType modType;
    public float value;
}

[CreateAssetMenu(fileName = "Novo Rastro Upgrade", menuName = "ExoBeasts/Rastros/Rastro")]
public class RastroUpgrade : ScriptableObject
{
    [Header("Informações Básicas")]
    public string upgradeName; // Nome genérico do upgrade
    [TextArea] public string description;

    [Header("Modificadores de Status (Números)")]
    [Tooltip("Lista de status numéricos (Dano, Vida, etc)")]
    public List<CharacterStatModifier> modifiers;

    [Header("--- CONFIGURAÇÃO ESPECIAL ---")]
    [Tooltip("Use esta seção se o upgrade for do tipo 'Especial' (Mecânica única)")]

    // Nome específico do efeito especial (ex: "Rastro de Fogo")
    public string specialEffectName;

    // A script / prefab que vai ser criada
    [Tooltip("Arraste aqui o PREFAB ou SCRIPT com a lógica do especial.")]
    public MonoBehaviour specialBehaviorPrefab;

    // A imagem específica desse especial
    [Tooltip("Ícone exclusivo para este efeito especial.")]
    public Sprite specialIcon;

    // Função auxiliar para saber se esse upgrade é especial
    public bool IsSpecial()
    {
        return specialBehaviorPrefab != null;
    }
}