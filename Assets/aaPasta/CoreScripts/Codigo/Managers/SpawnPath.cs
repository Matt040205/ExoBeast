// NOVO: Uma classe para agrupar um ponto de spawn com sua respectiva rota.
// [System.Serializable] faz com que ela apareça no Inspector da Unity.
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class SpawnPath
{
    public string pathName; // Um nome para identificar a rota no Inspector
    public Transform spawnPoint;
    public List<Transform> patrolPoints;
}