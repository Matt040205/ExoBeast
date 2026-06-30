using UnityEngine;
using System.Collections.Generic;
using System.Linq;

[System.Serializable]
public struct StatIconEntry
{
    public CharacterStatType stat;
    public Sprite icon;
}

[CreateAssetMenu(fileName = "Novo Banco de Icones", menuName = "ExoBeasts/Rastros/Base de imagens")]
public class StatIconDatabase : ScriptableObject
{
    [Header("Mapeamento de Ícones")]
    public List<StatIconEntry> statIcons;

    private Dictionary<CharacterStatType, Sprite> _iconMap;

    public Sprite GetIconForStat(CharacterStatType statType)
    {
        if (_iconMap == null)
        {
            _iconMap = new Dictionary<CharacterStatType, Sprite>();
            foreach (var entry in statIcons)
            {
                if (entry.icon != null && !_iconMap.ContainsKey(entry.stat))
                {
                    _iconMap.Add(entry.stat, entry.icon);
                }
            }
        }

        if (_iconMap.ContainsKey(statType))
        {
            return _iconMap[statType];
        }

        return null;
    }
}