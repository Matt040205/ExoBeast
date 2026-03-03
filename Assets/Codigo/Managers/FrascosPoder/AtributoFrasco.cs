using UnityEngine;

public enum AtributoFrasco
{
    MaisVida,
    MaisDano,
    MaisTaxaCritica,
    MaisDanoCritico,
    MaisVelocidade,
    MaisVelocidadeAtaque,
    MaisArmadura,
    MaisPenetracaoArmadura,
    MaisAlcanceAtaque,
    MaisVelocidadeRecarga,
    MaisCargaUltimate
}

[CreateAssetMenu(fileName = "Novo Frasco de Poder", menuName = "ScriptableObjects/Itens/Frasco de Poder")]
public class FrascoDePoderSO : ScriptableObject
{
    [Header("Identificação")]
    public string nomeDoFrasco;
    [TextArea(3, 5)]
    public string descricao;

    [Header("Efeito")]
    public AtributoFrasco atributoAlvo;
    public float valorDoBonus;
}