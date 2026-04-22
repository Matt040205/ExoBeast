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

    [Header("VFX de Construção")]
    [SerializeField] private GameObject spawnBeamVfxPrefab;

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

            // TUTORIAL: USE_SKILLS toca logo depos de sair do build mode
            if (TutorialManager.Instance != null && GameDataManager.Instance != null)
            {
                if (!GameDataManager.Instance.tutoriaisConcluidos.Contains("USE_SKILLS"))
                    TutorialManager.Instance.TriggerTutorial("USE_SKILLS");
            }
        }
        else
        {
            // TUTORIAL: Primeira vez entrando no build mode
            if (TutorialManager.Instance != null)
            {
                if (!GameDataManager.Instance.tutoriaisConcluidos.Contains("HOW_TO_BUILD"))
                    TutorialManager.Instance.TriggerTutorial("HOW_TO_BUILD");
                else if (GameDataManager.Instance.tutoriaisConcluidos.Contains("EXPLAIN_UPGRADE"))
                    TutorialManager.Instance.TriggerTutorial("HOW_TO_UPGRADE");
            }
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

        Vector3 finalPosition = currentBuildGhost.transform.position;
        finalPosition.x = Mathf.Round(finalPosition.x / gridSize) * gridSize;
        finalPosition.z = Mathf.Round(finalPosition.z / gridSize) * gridSize;

        // Zero-latency VFX: O cliente que clicou vê o feixe instantaneamente
        if (spawnBeamVfxPrefab != null)
        {
            GlobalVFXPool.GetVFX(spawnBeamVfxPrefab, finalPosition, Quaternion.identity, 3f);
        }

        // Verificar se e armadilha ou torre (armadilhas nao estao em buildablePrefabs)
        if (selectedBuildableData is TrapDataSO trapDataSO)
        {
            int trapIndex = availableTraps.IndexOf(trapDataSO);
            if (trapIndex == -1) return;
            RequestPlaceTrapServerRpc(trapIndex, finalPosition, selectedBuildableCost);
        }
        else
        {
            int prefabIndex = buildablePrefabs.IndexOf(selectedBuildablePrefab);
            if (prefabIndex == -1) return;
            RequestPlaceBuildingServerRpc(prefabIndex, finalPosition, selectedBuildableCost);
        }

        ClearSelection();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceTrapServerRpc(int trapIndex, Vector3 pos, int cost, ServerRpcParams rpcParams = default)
    {
        if (trapIndex < 0 || trapIndex >= availableTraps.Count) return;
        if (!CurrencyManager.Instance.HasEnoughCurrency(cost, CurrencyType.Geodites)) return;

        TrapDataSO trapData = availableTraps[trapIndex];
        if (trapData == null || trapData.prefab == null) return;

        if (trapData.buildLimit > 0 && GetTrapCount(trapData) >= trapData.buildLimit) return;

        CurrencyManager.Instance.SpendCurrency(cost, CurrencyType.Geodites);

        // Dispara o ClientRpc para os outros jogadores verem o feixe antes da torre spawnar
        PlaySpawnBeamClientRpc(pos, rpcParams.Receive.SenderClientId);

        StartCoroutine(SpawnTrapWithDelay(trapData, pos));
    }

    private IEnumerator SpawnTrapWithDelay(TrapDataSO trapData, Vector3 pos)
    {
        yield return new WaitForSeconds(0.05f); // Micro-delay

        // 1. Spawna o prefab VISUAL
        GameObject newTrap = Instantiate(trapData.prefab, pos, Quaternion.identity);
        if (newTrap.TryGetComponent<NetworkObject>(out var netObj))
            netObj.Spawn();

        // 2. Spawna o prefab de LOGICA (onde ficam os scripts de efeito)
        if (trapData.logicPrefab != null)
        {
            GameObject logicObj = Instantiate(trapData.logicPrefab, pos, Quaternion.identity);

            TrapLogicBase logicBase = logicObj.GetComponent<TrapLogicBase>();
            if (logicBase != null) logicBase.trapData = trapData;

            if (logicObj.TryGetComponent<NetworkObject>(out var logicNetObj))
                logicNetObj.Spawn();
        }

        NotifyBuildingPlacedClientRpc(pos);
    }


    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceBuildingServerRpc(int prefabIndex, Vector3 pos, int cost, ServerRpcParams rpcParams = default)
    {
        if (!CurrencyManager.Instance.HasEnoughCurrency(cost, CurrencyType.Geodites)) return;

        GameObject prefabToSpawn = buildablePrefabs[prefabIndex];

        CurrencyManager.Instance.SpendCurrency(cost, CurrencyType.Geodites);

        // Dispara o ClientRpc para os outros jogadores verem o feixe antes da torre spawnar
        PlaySpawnBeamClientRpc(pos, rpcParams.Receive.SenderClientId);

        StartCoroutine(SpawnBuildingWithDelay(prefabToSpawn, pos));
    }

    private IEnumerator SpawnBuildingWithDelay(GameObject prefabToSpawn, Vector3 pos)
    {
        yield return new WaitForSeconds(0.05f); // Micro-delay

        GameObject newBuildObject = Instantiate(prefabToSpawn, pos, Quaternion.identity);

        if (newBuildObject.TryGetComponent<NetworkObject>(out var netObj))
        {
            netObj.Spawn();
        }

        NotifyBuildingPlacedClientRpc(pos);
    }

    [ClientRpc]
    private void PlaySpawnBeamClientRpc(Vector3 pos, ulong senderId)
    {
        // Se quem mandou foi o próprio cliente local, ignora (pois ele já tocou Zero-Latency no PlaceBuilding)
        if (NetworkManager.Singleton.LocalClientId == senderId) return;

        if (spawnBeamVfxPrefab != null)
        {
            GlobalVFXPool.GetVFX(spawnBeamVfxPrefab, pos, Quaternion.identity, 3f);
        }
    }

    [ClientRpc]
    private void NotifyBuildingPlacedClientRpc(Vector3 pos)
    {
        // TUTORIAL: Primeira torre colocada -> explica como sair do build mode
        if (TutorialManager.Instance != null)
        {
            TutorialManager.Instance.TriggerTutorial("RETURN_TO_COMMANDER");
        }

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

        int meuStartSlot = 0;
        int meuEndSlot = equipe != null ? equipe.Length - 1 : 7;

        if (NetworkManager.Singleton != null && (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer))
        {
            if (ExoBeasts.Multiplayer.Lobby.LobbyManager.Instance != null && ExoBeasts.Multiplayer.Auth.SessionManager.Instance != null)
            {
                var membros = ExoBeasts.Multiplayer.Lobby.LobbyManager.Instance.GetMembers();
                string meuId = ExoBeasts.Multiplayer.Auth.SessionManager.Instance.GetUserId();
                int meuIndice = membros.FindIndex(m => m.productUserId == meuId);
                int total = membros.Count;

                if (meuIndice != -1)
                {
                    if (total == 2) { meuStartSlot = meuIndice * 4; meuEndSlot = meuStartSlot + 3; }
                    else if (total == 3)
                    {
                        if (meuIndice == 0) { meuStartSlot = 0; meuEndSlot = 3; }
                        else if (meuIndice == 1) { meuStartSlot = 4; meuEndSlot = 5; }
                        else { meuStartSlot = 6; meuEndSlot = 7; }
                    }
                    else if (total == 4) { meuStartSlot = meuIndice * 2; meuEndSlot = meuStartSlot + 1; }
                }
            }
        }

        if (equipe != null)
        {
            for (int i = meuStartSlot; i <= meuEndSlot; i++)
            {
                if (i >= equipe.Length) break;

                CharacterBase personagem = equipe[i];
                
                // Ignora o primeiro slot do range local, pois ele é sempre o seu Comandante
                bool isMyCommanderSlot = (i == meuStartSlot);

                if (personagem != null && !isMyCommanderSlot && personagem.towerPrefab != null)
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