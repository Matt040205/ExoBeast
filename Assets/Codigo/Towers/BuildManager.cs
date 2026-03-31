using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// ── BuildManager ───────────────────────────────────────
/// Gerencia construcao de torres e armadilhas com autoridade no servidor.
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
    public List<GameObject> buildablePrefabs = new List<GameObject>();

    [Header("Armadilhas Disponíveis")]
    public List<TrapDataSO> availableTraps = new List<TrapDataSO>();

    [Header("Dono Local")]
    public static bool isBuildingMode = false;

    private GameObject currentBuildGhost;
    private GameObject selectedBuildablePrefab;
    private object selectedBuildableData;
    private int selectedBuildableCost;
    private bool isCurrentPlacementValid = false;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
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
            isCurrentPlacementValid = isOverValidSurface && hasEnoughCurrency && isBuildAllowed;
            ghostRenderer.material = (isCurrentPlacementValid) ? validPlacementMaterial : invalidPlacementMaterial;
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

    // =================================================================
    // O RADAR BRUTO: Procura e conta tudo que existe fisicamente no mapa!
    // =================================================================
    public int GetTrapCount(TrapDataSO trapData)
    {
        if (trapData == null || trapData.prefab == null) return 0;

        int count = 0;
        string baseName = trapData.prefab.name.Trim();

        GameObject[] allObjects = FindObjectsByType<GameObject>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);

        foreach (GameObject obj in allObjects)
        {
            // Tira o (Clone) maldito do Unity para os nomes baterem
            string objName = obj.name.Replace("(Clone)", "").Trim();

            if (objName == baseName)
            {
                // Garante que não está contando o "holograma verde" que você tá segurando no mouse
                if (currentBuildGhost != null && obj == currentBuildGhost) continue;

                count++;
            }
        }
        return count;
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
        if (!isCurrentPlacementValid) return;
        if (selectedBuildablePrefab == null || currentBuildGhost == null) return;

        int prefabIndex = buildablePrefabs.IndexOf(selectedBuildablePrefab);
        if (prefabIndex == -1) return;

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

        GameObject prefabToSpawn = buildablePrefabs[prefabIndex];

        TrapDataSO trapData = availableTraps.Find(t => t.prefab == prefabToSpawn);
        if (trapData != null && trapData.buildLimit > 0)
        {
            if (GetTrapCount(trapData) >= trapData.buildLimit) return;
        }

        CurrencyManager.Instance.SpendCurrency(cost, CurrencyType.Geodites);

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
        // Espera 0.2 segundos para garantir que o objeto brotou no mapa antes de mandar a UI contar
        StartCoroutine(UpdateUIAfterSpawn());
    }

    private IEnumerator UpdateUIAfterSpawn()
    {
        yield return new WaitForSeconds(0.2f);

        if (GameDataManager.Instance != null)
        {
            SetAvailableTowers(GameDataManager.Instance.equipeSelecionada);
        }
    }

    public bool IsHoldingBuilding => currentBuildGhost != null;

    public void SetAvailableTowers(CharacterBase[] equipe)
    {
        List<CharacterBase> torres = new List<CharacterBase>();

        if (equipe != null)
        {
            foreach (CharacterBase personagem in equipe)
            {
                if (personagem != null && !personagem.isCommander && personagem.towerPrefab != null)
                {
                    torres.Add(personagem);

                    if (!buildablePrefabs.Contains(personagem.towerPrefab))
                        buildablePrefabs.Add(personagem.towerPrefab);
                }
            }
        }

        if (availableTraps != null)
        {
            foreach (TrapDataSO trap in availableTraps)
            {
                if (trap != null && trap.prefab != null && !buildablePrefabs.Contains(trap.prefab))
                {
                    buildablePrefabs.Add(trap.prefab);
                }
            }
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateBuildUI(torres, availableTraps);
        }
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