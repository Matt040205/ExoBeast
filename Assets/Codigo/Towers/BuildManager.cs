using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using Unity.Cinemachine;
using FMODUnity;
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Multiplayer.Sync;

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

    [Header("Área Jogável")]
    [Tooltip("Collider (trigger) que define os limites do mapa. Armadilhas e torres só podem ser colocadas dentro desta área. Deixe vazio para desabilitar o check.")]
    public Collider playableAreaBounds;

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
        isBuildingMode = false;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        ForceBuildMode(false);
        // Inicializa o UI de build em todos os clientes ao entrar na cena.
        // Sem isso, clientes remotos só recebem SetAvailableTowers após a primeira
        // construção (via NotifyBuildingPlacedClientRpc), deixando os tooltips vazios.
        StartCoroutine(InitBuildUIWhenReady());
    }

    private IEnumerator InitBuildUIWhenReady()
    {
        // Espera GameDataManager existir E pelo menos um slot de equipe estar preenchido.
        // O array equipeSelecionada sempre existe (8 slots null), então checar apenas
        // != null não é suficiente — em multiplayer, os slots são preenchidos no SelecaoManager
        // e podem ainda estar vazios quando esta cena carrega.
        float elapsed = 0f;
        const float timeout = 5f;

        yield return new WaitUntil(() =>
        {
            elapsed += Time.deltaTime;
            if (elapsed >= timeout) return true; // timeout — tenta com o que tiver

            if (GameDataManager.Instance == null) return false;
            var equipe = GameDataManager.Instance.equipeSelecionada;
            if (equipe == null) return false;

            // Verifica se pelo menos um slot está preenchido
            foreach (var slot in equipe)
                if (slot != null) return true;

            return false;
        });

        if (GameDataManager.Instance != null)
            SetAvailableTowers(GameDataManager.Instance.equipeSelecionada);
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
        ForceBuildMode(!isBuildingMode);
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

    public void ForceBuildMode(bool state)
    {
        isBuildingMode = state;
        ToggleBuildMode(state);
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
            SanitizeGhostPreview(currentBuildGhost);

            var towerController = currentBuildGhost.GetComponentInChildren<TowerController>();
            if (towerController) towerController.enabled = false;

            foreach (var col in currentBuildGhost.GetComponentsInChildren<Collider>()) col.enabled = false;
        }

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        bool isOverValidSurface = false;

        // Desativa temporariamente o collider da Área Jogável para que o raycast
        // nunca bata nele, não importa em qual Layer o usuário o colocou.
        if (playableAreaBounds != null) playableAreaBounds.enabled = false;

        bool raycastHit = Physics.Raycast(ray, out hit, 1000f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        
        if (playableAreaBounds != null) playableAreaBounds.enabled = true;

        if (raycastHit)
        {
            isOverValidSurface = GridPlacement.IsPlacementValid(hit, selectedBuildableData as ScriptableObject);

            // Bloqueia placement fora da área jogável do mapa
            if (isOverValidSurface && !IsInsidePlayableArea(hit.point))
                isOverValidSurface = false;

            float calculatedHeight = CalculateRequiredHeight(hit.point, selectedBuildablePrefab);
            currentBuildGhost.transform.position = new Vector3(hit.point.x, hit.point.y + calculatedHeight, hit.point.z);
        }
        else
        {
            currentBuildGhost.transform.position = ray.GetPoint(20f);
        }

        bool hasEnoughCurrency = HasEnoughBuildCurrency(selectedBuildableData);
        bool isBuildAllowed = IsBuildAllowedLocal(selectedBuildableData);

        var ghostRenderer = currentBuildGhost.GetComponentInChildren<MeshRenderer>();
        if (ghostRenderer != null)
        {
            isCurrentPlacementValid = isOverValidSurface && hasEnoughCurrency && isBuildAllowed;
            ghostRenderer.material = (isCurrentPlacementValid) ? validPlacementMaterial : invalidPlacementMaterial;
        }
    }

    /// <summary>
    /// Verifica se um ponto do mundo está dentro da área jogável definida pelo
    /// collider playableAreaBounds, checando APENAS eixos X e Z (horizontal).
    /// Isso evita bugs se o cubo estiver um pouco acima ou abaixo do chão real.
    /// </summary>
    private bool IsInsidePlayableArea(Vector3 worldPoint)
    {
        if (playableAreaBounds == null) return true;

        Bounds b = playableAreaBounds.bounds;
        return (worldPoint.x >= b.min.x && worldPoint.x <= b.max.x &&
                worldPoint.z >= b.min.z && worldPoint.z <= b.max.z);
    }

    private bool IsBuildAllowedLocal(object buildableData)
    {
        if (buildableData is TrapDataSO trapData)
        {
            if (trapData.buildLimit > 0 && GetTrapCount(trapData) >= trapData.buildLimit) return false;
        }
        return true;
    }

    private bool HasEnoughBuildCurrency(object buildableData)
    {
        if (CurrencyManager.Instance == null)
            return false;

        if (buildableData is TrapDataSO trapData)
        {
            return CurrencyManager.Instance.HasEnoughCurrency(trapData.geoditeCost, CurrencyType.Geodites) &&
                   CurrencyManager.Instance.HasEnoughCurrency(trapData.darkEtherCost, CurrencyType.DarkEther);
        }

        return CurrencyManager.Instance.HasEnoughCurrency(selectedBuildableCost, CurrencyType.Geodites);
    }

    // =================================================================
    // O RADAR BRUTO: Procura e conta tudo que existe fisicamente no mapa!
    // =================================================================
    public int GetTrapCount(TrapDataSO trapData)
    {
        if (trapData == null || trapData.prefab == null) return 0;

        NetworkedTrapVisual[] networkedTraps = FindObjectsByType<NetworkedTrapVisual>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        if (networkedTraps != null && networkedTraps.Length > 0)
        {
            int networkedCount = 0;
            foreach (NetworkedTrapVisual trapVisual in networkedTraps)
            {
                if (trapVisual == null) continue;

                TrapDataSO resolvedData = trapVisual.TrapData;
                if (resolvedData == trapData ||
                    (resolvedData != null && resolvedData.prefab != null && resolvedData.prefab.name == trapData.prefab.name))
                    networkedCount++;
            }

            return networkedCount;
        }

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
        else if (selectedBuildableData is CharacterBase towerData)
        {
            int characterIndex = GetCharacterLibraryIndex(towerData);
            if (characterIndex < 0) return;
            RequestPlaceBuildingServerRpc(characterIndex, finalPosition, selectedBuildableCost);
        }

        ClearSelection();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceTrapServerRpc(int trapIndex, Vector3 pos, int cost, ServerRpcParams rpcParams = default)
    {
        if (trapIndex < 0 || trapIndex >= availableTraps.Count) return;

        TrapDataSO trapData = availableTraps[trapIndex];
        if (trapData == null || trapData.prefab == null) return;
        if (!CurrencyManager.Instance.HasEnoughCurrency(trapData.geoditeCost, CurrencyType.Geodites)) return;
        if (!CurrencyManager.Instance.HasEnoughCurrency(trapData.darkEtherCost, CurrencyType.DarkEther)) return;

        if (trapData.buildLimit > 0 && GetTrapCount(trapData) >= trapData.buildLimit) return;

        if (trapData.geoditeCost > 0)
            CurrencyManager.Instance.SpendCurrency(trapData.geoditeCost, CurrencyType.Geodites);

        if (trapData.darkEtherCost > 0)
            CurrencyManager.Instance.SpendCurrency(trapData.darkEtherCost, CurrencyType.DarkEther);

        // Dispara o ClientRpc para os outros jogadores verem o feixe antes da torre spawnar
        PlaySpawnBeamClientRpc(pos, rpcParams.Receive.SenderClientId);

        StartCoroutine(SpawnTrapWithDelay(trapData, trapIndex, pos, rpcParams.Receive.SenderClientId));
    }

    private IEnumerator SpawnTrapWithDelay(TrapDataSO trapData, int trapIndex, Vector3 pos, ulong builderClientId)
    {
        yield return new WaitForSeconds(0.05f); // Micro-delay

        NetworkObject logicNetObj = null;
        TrapLogicBase logicBase = null;
        if (trapData.logicPrefab != null)
        {
            if (!ValidateNetworkSpawnable(trapData.logicPrefab, $"{trapData.name} logic"))
                yield break;

            GameObject logicObj = Instantiate(trapData.logicPrefab, pos, Quaternion.identity);

            logicBase = logicObj.GetComponent<TrapLogicBase>();
            if (logicBase != null)
                logicBase.InitializeServer(trapData, builderClientId, 0);

            if (logicObj.TryGetComponent<NetworkObject>(out var spawnedLogicNetObj))
            {
                spawnedLogicNetObj.Spawn();
                logicNetObj = spawnedLogicNetObj;
            }
        }

        if (!ValidateNetworkSpawnable(trapData.prefab, trapData.name))
        {
            if (logicNetObj != null && logicNetObj.IsSpawned)
                logicNetObj.Despawn(true);
            yield break;
        }

        GameObject newTrap = Instantiate(trapData.prefab, pos, Quaternion.identity);
        if (newTrap.TryGetComponent<NetworkObject>(out var netObj))
        {
            NetworkedTrapVisual networkedTrapVisual = newTrap.GetComponent<NetworkedTrapVisual>();
            if (networkedTrapVisual != null)
            {
                ulong logicObjectId = logicNetObj != null ? logicNetObj.NetworkObjectId : 0;
                networkedTrapVisual.InitializeServer(builderClientId, trapIndex, logicObjectId);
            }

            netObj.Spawn();

            if (logicBase != null)
                logicBase.BindVisualServer(netObj.NetworkObjectId);
        }

        NotifyBuildingPlacedClientRpc(pos);
    }


    [ServerRpc(RequireOwnership = false)]
    private void RequestPlaceBuildingServerRpc(int characterIndex, Vector3 pos, int cost, ServerRpcParams rpcParams = default)
    {
        if (!CurrencyManager.Instance.HasEnoughCurrency(cost, CurrencyType.Geodites)) return;

        var biblioteca = GameDataManager.Instance?.bibliotecaOriginalPersonagens;
        if (biblioteca == null || characterIndex < 0 || characterIndex >= biblioteca.Count) return;

        CharacterBase characterData = biblioteca[characterIndex];
        GameObject prefabToSpawn = characterData?.towerPrefab;

        if (prefabToSpawn == null)
        {
            Debug.LogError($"[BuildManager] Torre de índice {characterIndex} não possui towerPrefab válido. Verifique bibliotecaOriginalPersonagens e NetworkPrefabs.");
            return;
        }

        CurrencyManager.Instance.SpendCurrency(cost, CurrencyType.Geodites);

        // Dispara o ClientRpc para os outros jogadores verem o feixe antes da torre spawnar
        PlaySpawnBeamClientRpc(pos, rpcParams.Receive.SenderClientId);

        StartCoroutine(SpawnBuildingWithDelay(prefabToSpawn, characterIndex, cost, pos, rpcParams.Receive.SenderClientId));
    }

    private IEnumerator SpawnBuildingWithDelay(GameObject prefabToSpawn, int characterIndex, int cost, Vector3 pos, ulong builderClientId)
    {
        yield return new WaitForSeconds(0.05f); // Micro-delay

        if (!ValidateNetworkSpawnable(prefabToSpawn, prefabToSpawn.name))
            yield break;

        GameObject newBuildObject = Instantiate(prefabToSpawn, pos, Quaternion.identity);
        SanitizeRuntimeBuildable(newBuildObject, false);

        if (newBuildObject.TryGetComponent<NetworkObject>(out var netObj))
        {
            NetworkedBuilding networkedBuilding = newBuildObject.GetComponent<NetworkedBuilding>();
            if (networkedBuilding != null)
                networkedBuilding.InitializeTowerServer(builderClientId, characterIndex, cost);

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
        TryPopulateBuildUiFromCanonicalSlots(equipe);
    }

    public void ClearSelection()
    {
        if (currentBuildGhost != null) Destroy(currentBuildGhost);
        currentBuildGhost = null;
        selectedBuildablePrefab = null;
        selectedBuildableCost = 0;
        selectedBuildableData = null;
    }

    public TrapDataSO GetTrapDataByIndex(int trapIndex)
    {
        if (trapIndex < 0 || trapIndex >= availableTraps.Count)
            return null;

        return availableTraps[trapIndex];
    }

    private bool TryPopulateBuildUiFromCanonicalSlots(CharacterBase[] equipe)
    {
        List<CharacterBase> torres = new List<CharacterBase>();
        buildablePrefabs.RemoveAll(prefab => prefab == null);

        if (equipe != null)
        {
            foreach (int slot in ResolveLocalTowerSlots())
            {
                if (slot < 0 || slot >= equipe.Length) continue;

                CharacterBase personagem = equipe[slot];
                if (personagem == null || personagem.towerPrefab == null) continue;

                torres.Add(personagem);
                if (!buildablePrefabs.Contains(personagem.towerPrefab))
                    buildablePrefabs.Add(personagem.towerPrefab);
            }
        }

        if (availableTraps != null)
        {
            foreach (TrapDataSO trap in availableTraps)
            {
                if (trap != null && trap.prefab != null && !buildablePrefabs.Contains(trap.prefab))
                    buildablePrefabs.Add(trap.prefab);
            }
        }

        if (UIManager.Instance != null)
            UIManager.Instance.UpdateBuildUI(torres, availableTraps);

        return true;
    }

    private List<int> ResolveLocalTowerSlots()
    {
        if (NetworkManager.Singleton != null &&
            (NetworkManager.Singleton.IsClient || NetworkManager.Singleton.IsServer) &&
            ExoBeasts.Multiplayer.Lobby.LobbyManager.Instance != null &&
            ExoBeasts.Multiplayer.Auth.SessionManager.Instance != null)
        {
            var lobbyManager = ExoBeasts.Multiplayer.Lobby.LobbyManager.Instance;
            var membros = lobbyManager.GetOrderedMembers();
            string meuId = ExoBeasts.Multiplayer.Auth.SessionManager.Instance.GetUserId();
            int meuIndice = lobbyManager.GetCanonicalMemberIndex(meuId);

            if (meuIndice >= 0)
                return PartySlotLayout.GetTowerSlots(membros.Count, meuIndice);
        }

        return new List<int> { 1, 2, 3, 4, 5, 6, 7 };
    }

    private int GetCharacterLibraryIndex(CharacterBase towerData)
    {
        if (towerData == null || GameDataManager.Instance?.bibliotecaOriginalPersonagens == null)
            return -1;

        string cleanName = towerData.name.Replace("(Clone)", "");
        int index = GameDataManager.Instance.bibliotecaOriginalPersonagens.FindIndex(
            character => character != null && character.name == cleanName);

        if (index < 0)
            Debug.LogWarning($"[BuildManager] Torre '{towerData.name}' nao encontrada em bibliotecaOriginalPersonagens.");

        return index;
    }

    private void SanitizeGhostPreview(GameObject buildGhost)
    {
        SanitizeRuntimeBuildable(buildGhost, true);
    }

    private bool ValidateNetworkSpawnable(GameObject prefab, string context)
    {
        if (prefab == null)
            return false;

        if (!prefab.TryGetComponent<NetworkObject>(out _))
        {
            Debug.LogError($"[BuildManager] '{context}' precisa de NetworkObject para spawn autoritativo.");
            return false;
        }

        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return true;

        if (!NetworkManager.Singleton.NetworkConfig.Prefabs.Contains(prefab))
        {
            Debug.LogError($"[BuildManager] '{context}' nao esta registrado em DefaultNetworkPrefabs.");
            return false;
        }

        return true;
    }

    private void SanitizeRuntimeBuildable(GameObject buildableInstance, bool isPreview)
    {
        if (buildableInstance == null)
            return;

        foreach (PlayerInput playerInput in buildableInstance.GetComponentsInChildren<PlayerInput>(true))
            playerInput.enabled = false;

        foreach (LocalPlayerInputBridge inputBridge in buildableInstance.GetComponentsInChildren<LocalPlayerInputBridge>(true))
            inputBridge.enabled = false;

        foreach (PlayerMovement movement in buildableInstance.GetComponentsInChildren<PlayerMovement>(true))
            movement.enabled = false;

        foreach (PlayerShooting shooting in buildableInstance.GetComponentsInChildren<PlayerShooting>(true))
            shooting.enabled = false;

        foreach (MeleeCombatSystem melee in buildableInstance.GetComponentsInChildren<MeleeCombatSystem>(true))
            melee.enabled = false;

        foreach (PlayerCombatManager combatManager in buildableInstance.GetComponentsInChildren<PlayerCombatManager>(true))
            combatManager.enabled = false;

        foreach (CommanderAbilityController abilityController in buildableInstance.GetComponentsInChildren<CommanderAbilityController>(true))
            abilityController.enabled = false;

        foreach (CommanderController commanderController in buildableInstance.GetComponentsInChildren<CommanderController>(true))
            commanderController.enabled = false;

        foreach (PlayerHealthSystem healthSystem in buildableInstance.GetComponentsInChildren<PlayerHealthSystem>(true))
            healthSystem.enabled = false;

        foreach (PauseControl pauseControl in buildableInstance.GetComponentsInChildren<PauseControl>(true))
            pauseControl.enabled = false;

        foreach (PlayerNetworkSetup networkSetup in buildableInstance.GetComponentsInChildren<PlayerNetworkSetup>(true))
            networkSetup.enabled = false;

        foreach (NetworkedPlayerController networkedPlayerController in buildableInstance.GetComponentsInChildren<NetworkedPlayerController>(true))
            networkedPlayerController.enabled = false;

        foreach (CameraController cameraController in buildableInstance.GetComponentsInChildren<CameraController>(true))
            cameraController.enabled = false;

        foreach (ClientNetworkTransform networkTransform in buildableInstance.GetComponentsInChildren<ClientNetworkTransform>(true))
            networkTransform.enabled = false;

        foreach (CinemachineCamera cinematicCamera in buildableInstance.GetComponentsInChildren<CinemachineCamera>(true))
            cinematicCamera.enabled = false;

        foreach (AudioListener audioListener in buildableInstance.GetComponentsInChildren<AudioListener>(true))
            audioListener.enabled = false;

        foreach (StudioListener studioListener in buildableInstance.GetComponentsInChildren<StudioListener>(true))
            studioListener.enabled = false;

        foreach (CharacterController characterController in buildableInstance.GetComponentsInChildren<CharacterController>(true))
            characterController.enabled = false;

        foreach (CapsuleCollider capsuleCollider in buildableInstance.GetComponentsInChildren<CapsuleCollider>(true))
            capsuleCollider.enabled = false;

        if (!isPreview)
            return;

        foreach (Collider colliderComponent in buildableInstance.GetComponentsInChildren<Collider>(true))
            colliderComponent.enabled = false;
    }
}
