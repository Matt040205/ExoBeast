using UnityEngine;

public class GridPlacement : MonoBehaviour
{
    [Header("Configuração de Grade")]
    public float gridSize = 1f;

    [Header("Ajuste de Posicionamento")]
    public bool pivotIsAtFeet = true;

    [Header("Custo")]
    public int cost = 100;

    public float GetObjectHalfHeight()
    {
        if (pivotIsAtFeet)
        {
            return 0f;
        }

        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            return rend.bounds.extents.y;
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            return col.bounds.extents.y;
        }

        return 0f;
    }

    public void SnapToGrid()
    {
        Vector3 currentPosition = transform.position;

        float alignedX = Mathf.Round(currentPosition.x / gridSize) * gridSize;
        float alignedZ = Mathf.Round(currentPosition.z / gridSize) * gridSize;

        transform.position = new Vector3(alignedX, currentPosition.y, alignedZ);
    }

    public float GetObjectHeight()
    {
        Renderer rend = GetComponent<Renderer>();
        if (rend != null)
        {
            return rend.bounds.size.y;
        }

        Collider col = GetComponent<Collider>();
        if (col != null)
        {
            return col.bounds.size.y;
        }

        return 0f;
    }

    public static bool IsPlacementValid(RaycastHit hit, object buildableData)
    {
        if (buildableData is TrapDataSO trapData)
        {
            switch (trapData.placementType)
            {
                case TrapPlacementType.OnPath:
                    return hit.transform.CompareTag("Path");
                case TrapPlacementType.OffPath:
                    return hit.transform.CompareTag("Local");
                case TrapPlacementType.QualquerLugar:
                    return true;
            }
        }
        else if (buildableData is CharacterBase)
        {
            return hit.transform.CompareTag("Local");
        }

        return false;
    }
}