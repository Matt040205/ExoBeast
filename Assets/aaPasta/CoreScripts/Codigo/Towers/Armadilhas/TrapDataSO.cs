using UnityEngine;

public enum TrapPlacementType
{
    OnPath,
    OffPath,
    QualquerLugar
}

[CreateAssetMenu(fileName = "Nova Armadilha", menuName = "ScriptableObjects/Armadilha")]
public class TrapDataSO : ScriptableObject
{
    [Header("Informações da Armadilha")]
    public string trapName = "Nova Armadilha";
    [TextArea(3, 5)]
    public string description = "Descrição da armadilha.";
    public Sprite icon;

    [Tooltip("O prefab VISUAL da armadilha.")]
    public GameObject prefab;

    [Header("Lógica da Armadilha")]
    [Tooltip("Prefab que contém o SCRIPT (MonoBehaviour).")]
    public GameObject logicPrefab;

    [Header("Posicionamento")]
    public TrapPlacementType placementType = TrapPlacementType.OffPath;

    [Header("Custos")]
    public int geoditeCost = 10;
    public int darkEtherCost = 0;

    [Header("Limites")]
    public int buildLimit = 0;
}