using UnityEngine;
using System.Collections.Generic;
using UnityEngine.EventSystems;
using Unity.Netcode;

public class BuildManager : MonoBehaviour
{
    public static BuildManager Instance;

    [Header("Câmeras")]
    public GameObject upgradePanel;

    [Header("Listas de Construíveis")]
    private List<CharacterBase> availableTowers = new List<CharacterBase>();
    public List<TrapDataSO> availableTraps = new List<TrapDataSO>();
    private Dictionary<TrapDataSO, int> activeTrapCounts = new Dictionary<TrapDataSO, int>();

    private GameObject selectedBuildablePrefab;
    private int selectedBuildableCost;
    private object selectedBuildableData;

    [Header("Visual do Ghost")]
    private GameObject currentBuildGhost;
    public Material validPlacementMaterial;
    public Material invalidPlacementMaterial;

    public static bool isBuildingMode = false;
    public float gridSize = 1f;

    [Header("Configuração de Altura")]
    public float globalHeightOffset = 0.5f;

    public bool IsHoldingBuilding { get { return selectedBuildablePrefab != null; } }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public int GetTrapCount(TrapDataSO trapData)
    {
        if (activeTrapCounts.ContainsKey(trapData))
        {
            return activeTrapCounts[trapData];
        }
        return 0;
    }

    public void RegisterTrap(TrapDataSO trapData)
    {
        int count = GetTrapCount(trapData);
        activeTrapCounts[trapData] = count + 1;
        UpdateBuildUI();
    }

    public void DeregisterTrap(TrapDataSO trapData)
    {
        int count = GetTrapCount(trapData);
        if (count > 0)
        {
            activeTrapCounts[trapData] = count - 1;
        }
        UpdateBuildUI();
    }

    public void SetAvailableTowers(CharacterBase[] selectedTeam)
    {
        availableTowers.Clear();
        for (int i = 1; i < selectedTeam.Length; i++)
        {
            if (selectedTeam[i] != null)
            {
                availableTowers.Add(selectedTeam[i]);
            }
        }
        UpdateBuildUI();
    }

    public void UpdateBuildUI()
    {
        if (UIManager.Instance != null)
        {
            UIManager.Instance.UpdateBuildUI(availableTowers, availableTraps);
        }
    }

    public void SelectTowerToBuild(CharacterBase towerData)
    {
        if (TowerSelectionManager.Instance != null)
        {
            TowerSelectionManager.Instance.DeselectAll();
        }

        ClearSelection();
        selectedBuildablePrefab = towerData.towerPrefab;
        selectedBuildableCost = towerData.cost;
        selectedBuildableData = towerData;
    }

    public void SelectTrapToBuild(TrapDataSO trapData)
    {
        if (TowerSelectionManager.Instance != null)
        {
            TowerSelectionManager.Instance.DeselectAll();
        }

        ClearSelection();
        selectedBuildablePrefab = trapData.prefab;
        selectedBuildableCost = trapData.geoditeCost;
        selectedBuildableData = trapData;
    }

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.B))
        {
            isBuildingMode = !isBuildingMode;
            ToggleBuildMode(isBuildingMode);
        }

        if (isBuildingMode)
        {
            HandleBuildGhost();

            if (Input.GetMouseButtonDown(0))
            {
                if (EventSystem.current.IsPointerOverGameObject())
                {
                    return;
                }
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
        if (TopDownCameraManager.Instance != null)
        {
            TopDownCameraManager.Instance.ToggleTopDownView(state);
        }

        if (!state)
        {
            ClearSelection();

            if (TowerSelectionManager.Instance != null)
            {
                TowerSelectionManager.Instance.DeselectAll();
            }

            if (TutorialManager.Instance != null && GameDataManager.Instance != null && GameDataManager.Instance.tutoriaisConcluidos.Contains("RETURN_TO_COMMANDER"))
            {
                TutorialManager.Instance.TriggerTutorial("USE_SKILLS");
            }
        }
        else
        {
            if (TutorialManager.Instance != null && GameDataManager.Instance != null)
            {
                if (GameDataManager.Instance.tutoriaisConcluidos.Contains("EXPLAIN_UPGRADE"))
                {
                    TutorialManager.Instance.TriggerTutorial("HOW_TO_UPGRADE");
                }
                else if (GameDataManager.Instance.tutoriaisConcluidos.Contains("EXPLAIN_BUILD_MODE"))
                {
                    TutorialManager.Instance.TriggerTutorial("HOW_TO_BUILD");
                }
            }
        }

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowBuildUI(state);
        }
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

            Collider[] colliders = currentBuildGhost.GetComponentsInChildren<Collider>();
            foreach (var col in colliders) col.enabled = false;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        bool isOverValidSurface = false;

        if (Physics.Raycast(ray, out hit))
        {
            isOverValidSurface = GridPlacement.IsPlacementValid(hit, selectedBuildableData);

            if (isOverValidSurface)
            {
                float calculatedHeight = CalculateRequiredHeight(hit.point, selectedBuildablePrefab);
                currentBuildGhost.transform.position = new Vector3(hit.point.x, hit.point.y + calculatedHeight, hit.point.z);
            }
            else
            {
                currentBuildGhost.transform.position = hit.point + Vector3.up * globalHeightOffset;
            }
        }
        else
        {
            if (currentBuildGhost != null)
                currentBuildGhost.transform.position = ray.GetPoint(20f);
        }

        int buildingCost = selectedBuildableCost;
        bool hasEnoughCurrency = CurrencyManager.Instance.HasEnoughCurrency(buildingCost, CurrencyType.Geodites);
        bool isBuildAllowed = IsBuildAllowed(selectedBuildableData);

        var ghostRenderer = currentBuildGhost.GetComponentInChildren<MeshRenderer>();
        if (ghostRenderer != null)
        {
            ghostRenderer.material = (isOverValidSurface && hasEnoughCurrency && isBuildAllowed) ? validPlacementMaterial : invalidPlacementMaterial;
        }
    }

    private bool IsBuildAllowed(object buildableData)
    {
        if (buildableData is TrapDataSO trapData)
        {
            if (trapData.logicPrefab != null && trapData.logicPrefab.GetComponent<Teleportador>() != null)
            {
                if (Teleportador.GetPortalCount() >= 2)
                    return false;
            }

            if (trapData.buildLimit > 0)
            {
                if (GetTrapCount(trapData) >= trapData.buildLimit)
                    return false;
            }
        }
        return true;
    }

    private float CalculateRequiredHeight(Vector3 hitPoint, GameObject prefab)
    {
        Collider col = prefab.GetComponentInChildren<Collider>();
        if (col != null)
        {
            float bottomDistance = col.bounds.extents.y;
            Ray downRay = new Ray(hitPoint + Vector3.up * 10f, Vector3.down);
            RaycastHit downHit;

            if (Physics.Raycast(downRay, out downHit, 20f))
            {
                return downHit.point.y - hitPoint.y + bottomDistance + globalHeightOffset;
            }
        }
        return globalHeightOffset;
    }

    void PlaceBuilding()
    {
        if (selectedBuildablePrefab == null || currentBuildGhost == null) return;

        var ghostRenderer = currentBuildGhost.GetComponentInChildren<MeshRenderer>();

        if (ghostRenderer != null && ghostRenderer.material.name.StartsWith(validPlacementMaterial.name))
        {
            if (CurrencyManager.Instance.HasEnoughCurrency(selectedBuildableCost, CurrencyType.Geodites))
            {
                Vector3 finalPosition = currentBuildGhost.transform.position;
                finalPosition.x = Mathf.Round(finalPosition.x / gridSize) * gridSize;
                finalPosition.z = Mathf.Round(finalPosition.z / gridSize) * gridSize;

                GameObject newBuildObject = Instantiate(selectedBuildablePrefab, finalPosition, Quaternion.identity);

                if (newBuildObject.TryGetComponent<NetworkObject>(out NetworkObject netObj))
                {
                    netObj.Spawn();
                }

                TrapDataSO placedTrapData = null;

                if (selectedBuildableData is TrapDataSO trapData && trapData.logicPrefab != null)
                {
                    placedTrapData = trapData;
                    var logicComponentOnPrefab = trapData.logicPrefab.GetComponent<MonoBehaviour>();
                    if (logicComponentOnPrefab != null)
                    {
                        var newComponent = newBuildObject.AddComponent(logicComponentOnPrefab.GetType());

                        TrapLogicBase trapLogic = newComponent as TrapLogicBase;
                        if (trapLogic != null)
                        {
                            trapLogic.trapData = trapData;
                        }
                    }
                }

                CurrencyManager.Instance.SpendCurrency(selectedBuildableCost, CurrencyType.Geodites);

                if (placedTrapData != null)
                {
                    RegisterTrap(placedTrapData);
                }

                if (TutorialManager.Instance != null)
                {
                    TutorialManager.Instance.TriggerTutorial("RETURN_TO_COMMANDER");
                }

                ClearSelection();
            }
        }
    }

    public void ClearSelection()
    {
        if (currentBuildGhost != null)
        {
            Destroy(currentBuildGhost);
        }
        currentBuildGhost = null;
        selectedBuildablePrefab = null;
        selectedBuildableCost = 0;
        selectedBuildableData = null;
    }
}