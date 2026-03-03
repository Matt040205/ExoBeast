using System.Collections.Generic;
using UnityEngine;

public class InventarioFrascos : MonoBehaviour
{
    [Header("Configuração")]
    public CharacterBase statusBase;

    [Header("Frascos Equipados")]
    public List<FrascoDePoderSO> frascosAtivos = new List<FrascoDePoderSO>();

    /// <summary>
    /// Retorna o valor total de um atributo (Base + Bônus de todos os frascos)
    /// </summary>
    public float GetStatusTotal(AtributoFrasco tipo)
    {
        if (statusBase == null)
        {
            Debug.LogWarning("Status Base não foi atribuído no Inventário de Frascos!");
            return 0;
        }

        float valorBase = GetValorBase(tipo);
        float bonusTotal = 0;

        foreach (var frasco in frascosAtivos)
        {
            if (frasco != null && frasco.atributoAlvo == tipo)
            {
                bonusTotal += frasco.valorDoBonus;
            }
        }

        return valorBase + bonusTotal;
    }

    private float GetValorBase(AtributoFrasco tipo)
    {
        return tipo switch
        {
            AtributoFrasco.MaisVida => statusBase.maxHealth,
            AtributoFrasco.MaisDano => statusBase.damage,
            AtributoFrasco.MaisTaxaCritica => statusBase.critChance,
            AtributoFrasco.MaisDanoCritico => statusBase.critDamage,
            AtributoFrasco.MaisVelocidade => statusBase.moveSpeed,
            AtributoFrasco.MaisVelocidadeAtaque => statusBase.attackSpeed,
            AtributoFrasco.MaisArmadura => statusBase.armor,
            AtributoFrasco.MaisPenetracaoArmadura => statusBase.armorPenetration,
            AtributoFrasco.MaisAlcanceAtaque => statusBase.attackRange,
            AtributoFrasco.MaisVelocidadeRecarga => statusBase.reloadSpeed,
            AtributoFrasco.MaisCargaUltimate => statusBase.ultimateChargePerSecond,
            _ => 0
        };
    }
}