using UnityEngine;

/// <summary>
/// ScriptableObject que define uma modificação de gameplay para um mapa.
/// Pode ser positiva (buff para o jogador) ou negativa (debuff/desafio extra).
/// Crie via: Assets > Create > ScriptableObjects > Mapa > ModificacaoData
/// </summary>
[CreateAssetMenu(fileName = "ModificacaoData", menuName = "ScriptableObjects/Mapa/ModificacaoData")]
public class ModificacaoData : ScriptableObject
{
    [Header("Identificação")]
    [Tooltip("Descrição da modificação exibida no painel (ex: 'Inimigos 5% mais resistentes')")]
    [TextArea(1, 3)]
    public string descricao;

    [Tooltip("Se verdadeiro, esta modificação é positiva (favorável ao jogador). Se falso, é negativa.")]
    public bool isPositiva = true;
}
