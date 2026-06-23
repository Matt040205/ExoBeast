using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "Novo Caminho", menuName = "ScriptableObjects/Trilhas/Caminho")]
public class UpgradePath : ScriptableObject
{
    [Header("Informações do Caminho")]
    public string pathName = "Novo Caminho";

    [Tooltip("A lista de upgrades para este caminho.")]
    public List<Upgrade> upgradesInPath;
}