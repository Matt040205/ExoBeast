using UnityEngine;

public enum ModificacaoGameplayEffect
{
    Nenhum = 0,
    InimigosReforcados = 1,
    ImpostoTatico = 2,
    MunicaoDefeituosa = 3,
    AgilidadeInimiga = 4,
    DanoResidual = 5,
    FadigaCibernetica = 6,
    RecursosEscassos = 7,
    BlindagemLeve = 8,
    VisibilidadeReduzida = 9,
    NucleoFragil = 10,
    TirosCriticos = 11,
    MobilidadeAumentada = 12,
    EconomiaPositiva = 13,
    SobrecargaDeNucleo = 14,
    MiraLaser = 15,
    AlvosMinusculos = 16,
    ProtocoloKamikaze = 17,
    SilencioDeRadio = 18,
    MunicaoInfinitaTemporaria = 19,
    RouboDeVidaSincronizado = 20,
    OndasDeChoque = 21,
    Execucao = 22,
    EnxameMassivo = 23,
    FrenesiMortal = 24,
    Gigantismo = 25,
    AvancoImplacavel = 26,
    ModoOverclock = 27,
    BalaDePrata = 28,
    EconomiaHacker = 29,
    SegundaChance = 30,
    ReforcosTaticos = 31,
    DanoRefletido = 32
}

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

    [Header("Gameplay")]
    [Tooltip("Efeito aplicado na partida quando esta modificação é sorteada.")]
    public ModificacaoGameplayEffect efeito = ModificacaoGameplayEffect.Nenhum;

    [Tooltip("Valor principal do efeito. Para multiplicadores, use 1.10 = +10%.")]
    public float valor = 1f;

    [Tooltip("Valor auxiliar opcional. Ex.: duração em segundos.")]
    public float valorSecundario = 0f;
}
