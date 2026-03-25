using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ── BuildManager ───────────────────────────────────────
/// Gerencia construcao de torres e armadilhas com autoridade no servidor.
///
///  ▸ Owner: ghost preview local (zero lag), selecao de torre/trap
///  ▸ RequestPlaceBuildingServerRpc: servidor valida custo, spawna NetworkObject
///  ▸ Grid snapping e validacao de posicao via GridPlacement
///  ▸ buildablePrefabs[]: indexacao de prefabs para referencia por indice em RPCs
/// ─────────────────────────────────────────────────────
/// </summary>
public class BuildManager : NetworkBehaviour
{
    public static BuildManager Instance { get; private set; }

    [Header("Configurações do Grid")]
    public float gridSize = 1f;
    public float globalHeightOffset = 0.5f;

    [Header("Materiais de Feedback")]
    public Material validPlacementMaterial;
    public Material invalidPlacementMaterial;

    [Header("Network Buildables")]
    [Tooltip("Arraste aqui todos os prefabs de torres e armadilhas que podem ser construídos para indexação em rede.")]
    public List<GameObject> buildablePrefabs = new List<GameObject>();

    [Header("Dono Local")]
    public static bool isBuildingMode = false;

    private GameObject currentBuildGhost;
    private GameObject selectedBuildablePrefab;
    private object selectedBuildableData;
    private int selectedBuildableCost;

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public void SelectTowerToBuild(CharacterBase towerData)
    {
        if (TowerSelectionManager.Instance != null) TowerSelectionManager.Instance.DeselectAll();

        ClearSelection();
        selectedBuildablePrefab = towerData.towerPrefab;
        selectedBuildableCost = towerData.cost;
        selectedBuildableData = towerData;
    }

    public void SelectTrapToBuild(TrapDataSO trapData)
    {
        if (TowerSelectionManager.Instance != null) TowerSelectionManager.Instance.DeselectAll();

        ClearSelection();
        selectedBuildablePrefab = trapData.prefab;
        selectedBuildableCost = trapData.geoditeCost;
        selectedBuildableData = trapData;
    }

    public void OnBuild(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed) return;
        isBuildingMode = !isBuildingMode;
        ToggleBuildMode(isBuildingMode);
    }

    void Update()
    {
        if (isBuildingMode)
        {
            HandleBuildGhost();

            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current.IsPointerOverGameObject()) return;
                PlaceBuilding();
            }

            if (Input.GetMouseButtonDown(1))
            {
                ClearSelection();
            }
        }
    }

    void ToggleBuildMode(bool state)
    {
        if (TopDownCameraManager.Instance != null) TopDownCameraManager.Instance.ToggleTopDownView(state);

        if (!state)
        {
            ClearSelection();
            if (TowerSelectionManager.Instance != null) TowerSelectionManager.Instance.DeselectAll();
        }

        if (UIManager.Instance != null) UIManager.Instance.ShowBuildUI(state);
    }

    void HandleBuildGhost()
    {
        if (selectedBuildablePrefab == null)
        {
            if (currentBuildGhost != null) Destroy(currentBuildGhost);
            return;
        }

        if (currentBuildGhost == null)
        {
            currentBuildGhost = Instantiate(selectedBuildablePrefab);
            
            var towerController = currentBuildGhost.GetComponentInChildren<TowerController>();
            if (towerController) towerController.enabled = false;

            foreach (var col in currentBuildGhost.GetComponentsInChildren<Collider>()) col.enabled = false;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        bool isOverValidSurface = false;

        if (Physics.Raycast(ray, out hit))
        {
            isOverValidSurface = GridPlacement.IsPlacementValid(hit, selectedBuildableData as ScriptableObject);

            float calculatedHeight = CalculateRequiredHeight(hit.point, selectedBuildablePrefab);
            currentBuildGhost.transform.position = new Vector3(hit.point.x, hit.point.y + calculatedHeight, hit.point.z);
        }
        else
        {
            currentBuildGhost.transform.position = ray.GetPoint(20f);
        }

        bool hasEnoughCurrency = CurrencyManager.Instance.HasEnoughCurrency(selectedBuildableCost, CurrencyType.Geodites);
        bool isBuildAllowed = IsBuildAllowedLocal(selectedBuildableData);

        var ghostRenderer = currentBuildGhost.GetComponentInChildren<MeshRenderer>();
        if (ghostRenderer != null)
        {
            bool valid = isOverValidSurface && hasEnoughCurrency && isBuildAllowed;
            ghostRenderer.material = (valid) ? validPlacementMaterial : invalidPlacementMaterial;
        }
    }

    private bool IsBuildAllowedLocal(object buildableData)
    {
        if (buildableData is TrapDataSO trapData)
        {
            if (trapData.buildLimit > 0 && GetTrapCount(trapData) >= trapData.buildLimit) return false;
        }
        return true;
    }

    public int GetTrapCount(TrapDataSO trapData)
    {
        // TODO: Em multiplayer, o servidor deveria expor essa contagem via NetworkVariable ou similar.
        // Por enquanto retornamos 0 ou mantemos local.
        return 0; 
    }

    private float CalculateRequiredHeight(Vector3 hitPoint, GameObject prefab)
    {
        Collider col = prefab.GetComponentInChildren<Collider>();
        if (col != null)
        {
            float bottomDistance = col.bounds.extents.y;
            return bottomDistance + globalHeightOffset;
        }
        return globalHeightOffset;
    }

    void PlaceBuilding()
    {
        if (selectedBuildablePrefab == null || currentBuildGhost == null) return;

        int prefabIndex = buildablePrefabs.IndexOf(selectedBuildablePrefab);
        if (prefabIndex == -1)
        {
            Debug.LogError("[BuildManager] Prefab selecionado não está na lista de BuildablePrefabs do BuildManager!");
            return;
        }

        Vector3 finalPosition = currentBuildGhost.transform.position;
        finalPosition.x = Mathf.Round(finalPosition.x / gridSize) * gridSize;
        finalPosition.z = Mathf.Round(finalPosition.z / gridSize) * gridSize;

        RequestPlaceBuildingServerRpc(prefabIndex, finalPosition, selectedBuildableCost);
        
        ClearSelection();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceBuildingServerRpc(int prefabIndex, Vector3 pos, int cost, ServerRpcParams rpcParams = default)
    {
        if (!CurrencyManager.Instance.HasEnoughCurrency(cost, CurrencyType.Geodites)) return;

        CurrencyManager.Instance.SpendCurrency(cost, CurrencyType.Geodites);

        GameObject prefabToSpawn = buildablePrefabs[prefabIndex];
        GameObject newBuildObject = Instantiate(prefabToSpawn, pos, Quaternion.identity);

        if (newBuildObject.TryGetComponent<NetworkObject>(out var netObj))
        {
            netObj.Spawn();
        }

        NotifyBuildingPlacedClientRpc(pos);
    }

    [ClientRpc]
    private void NotifyBuildingPlacedClientRpc(Vector3 pos)
    {
        // TODO: Tocar som ou spawnar efeito de partícula de construção
    }

    public bool IsHoldingBuilding => currentBuildGhost != null;

    public void SetAvailableTowers(CharacterBase[] equipe)
    {
        // Popula buildablePrefabs a partir da equipe selecionada
        // Os prefabs de torre já devem estar na lista; este método é um hook para futuras restrições
    }

    public void ClearSelection()
    {
        if (currentBuildGhost != null) Destroy(currentBuildGhost);
        currentBuildGhost = null;
        selectedBuildablePrefab = null;
        selectedBuildableCost = 0;
        selectedBuildableData = null;
    }
}

