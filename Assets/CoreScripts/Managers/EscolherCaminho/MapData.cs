using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject que define os dados de um mapa/fase disponível na tela de seleção de caminho.
/// Crie via: Assets > Create > ScriptableObjects > Mapa > MapData
/// </summary>
[CreateAssetMenu(fileName = "MapData", menuName = "ScriptableObjects/Mapa/MapData")]
public class MapData : ScriptableObject
{
    [Header("Identificação do Mapa")]
    [Tooltip("Nome do mapa exibido no painel de informações")]
    public string mapName = "Mapa sem Nome";

    [Tooltip("Foto/preview do mapa exibida no painel de informações")]
    public Sprite mapPhoto;

    [Header("Destino")]
    [Tooltip("Nome exato da cena Unity a carregar ao confirmar este caminho (ex: CenaMapaNOVO)")]
    public string destinationScene = "CenaMapaNOVO";

    [Header("Monstros")]
    [Tooltip("Lista de inimigos que aparecem neste mapa (usa os EnemyDataSO já existentes no projeto)")]
    public List<EnemyDataSO> monstros = new List<EnemyDataSO>();

    [Header("Pool de Modificações")]
    [Tooltip("Pool de modificações positivas possíveis para este mapa (uma será sorteada se aplicável)")]
    public List<ModificacaoData> poolPositivas = new List<ModificacaoData>();

    [Tooltip("Pool de modificações negativas possíveis para este mapa (uma será sorteada se aplicável)")]
    public List<ModificacaoData> poolNegativas = new List<ModificacaoData>();
}
