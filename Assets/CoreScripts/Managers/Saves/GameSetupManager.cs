using UnityEngine;
using Unity.Netcode;
using Unity.Netcode.Components;
using System.Collections.Generic;
using ExoBeasts.Multiplayer.GameServer;
using ExoBeasts.Multiplayer.Core;
using ExoBeasts.Multiplayer.Sync;

/// <summary>
/// ── GameSetupManager ───────────────────────────────────
/// Responsavel por spawnar o prefab do jogador quando um cliente conecta.
/// ─────────────────────────────────────────────────────
/// </summary>
public class GameSetupManager : NetworkBehaviour
{
    public static GameSetupManager Instance { get; private set; }

    [Header("Spawn Configs")]
    [Tooltip("Não precisa mais preencher os prefabs aqui! O código puxa direto do GameDataManager.")]
    public Transform[] spawnPoints;

    [Header("References")]
    public Transform spawnPoint;

    // Guarda anti-duplo-spawn: preenchido assim que SpawnPlayerServerSide comeca a processar
    // o clientId e antes de qualquer await/instanciacao. Evita race entre OnNetworkSpawn (host)
    // e OnClientConnectedCallback disparando para o mesmo clientId na mesma frame.
    private readonly HashSet<ulong> _spawnedClientIds = new HashSet<ulong>();
    private readonly List<Transform> spawnedPlayerTargets = new List<Transform>(4);

    public static bool TryResolveRespawnPose(string pointNameOrTag, out Vector3 position, out Quaternion rotation)
    {
        if (TryResolveRespawnTransform(pointNameOrTag, out Transform resolvedTransform))
        {
            position = resolvedTransform.position;
            rotation = resolvedTransform.rotation;
            return true;
        }

        position = Vector3.zero;
        rotation = Quaternion.identity;
        return false;
    }

    public static bool TryResolveRespawnTransform(string pointNameOrTag, out Transform resolvedTransform)
    {
        resolvedTransform = null;

        if (Instance != null)
        {
            if (IsUsableRespawnTransform(Instance.spawnPoint))
            {
                resolvedTransform = Instance.spawnPoint;
                return true;
            }

            if (Instance.spawnPoints != null)
            {
                foreach (Transform spawnCandidate in Instance.spawnPoints)
                {
                    if (!IsUsableRespawnTransform(spawnCandidate))
                        continue;

                    resolvedTransform = spawnCandidate;
                    return true;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(pointNameOrTag))
            return false;

        GameObject respawnObject = null;
        try
        {
            respawnObject = GameObject.FindWithTag(pointNameOrTag);
        }
        catch (UnityException)
        {
            // Nem todas as cenas declaram RespawnPoint como tag; nesses casos usamos nome/path.
        }

        if (respawnObject == null)
            respawnObject = GameObject.Find(pointNameOrTag);

        if (respawnObject != null && IsUsableRespawnTransform(respawnObject.transform))
        {
            resolvedTransform = respawnObject.transform;
            return true;
        }

        Transform[] sceneTransforms = FindObjectsByType<Transform>(
            FindObjectsInactive.Exclude,
            FindObjectsSortMode.None);

        foreach (Transform sceneTransform in sceneTransforms)
        {
            if (!IsUsableRespawnTransform(sceneTransform))
                continue;

            string transformName = sceneTransform.name;
            if (transformName == pointNameOrTag || transformName.StartsWith(pointNameOrTag + "/", System.StringComparison.Ordinal))
            {
                resolvedTransform = sceneTransform;
                return true;
            }
        }

        return false;
    }

    private static bool IsUsableRespawnTransform(Transform respawnTransform)
    {
        return respawnTransform != null && respawnTransform.gameObject.activeInHierarchy;
    }

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback += OnClientDisconnected;
            // Spawna todos os clientes já conectados (cobre o caso onde NGO iniciou em cena anterior,
            // como CenaSeleçao, e OnClientConnected não dispara novamente ao carregar esta cena)
            foreach (var clientId in NetworkManager.Singleton.ConnectedClientsIds)
                SpawnPlayerServerSide(clientId);

            ValidateObjectiveHealthSetup();
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        SpawnPlayerServerSide(clientId);
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (!IsServer) return;
        // Libera a entry do dedupe para permitir respawn se o cliente reconectar.
        _spawnedClientIds.Remove(clientId);
    }

    private void SpawnPlayerServerSide(ulong clientId)
    {
        if (!IsServer) return;

        // Dedupe — cobre os dois caminhos que podem disparar para o host:
        //   (1) OnNetworkSpawn chama SpawnPlayerServerSide(LocalClientId) explicitamente.
        //   (2) OnClientConnectedCallback(0) dispara quando o host se "conecta" a si mesmo.
        // Adicionar ao HashSet ANTES de qualquer Instantiate/RegisterPlayer — o check do
        // PlayerRegistry nao e suficiente porque ele so e populado no fim deste metodo.
        if (!_spawnedClientIds.Add(clientId))
        {
            Debug.Log($"[GameSetupManager] Spawn para clientId={clientId} ja em progresso/concluido. Ignorando chamada duplicada.");
            return;
        }

        if (HasExistingPlayerObject(clientId))
        {
            RefreshEnemyTargetsAfterPlayerRegistration(clientId);
            return;
        }

        GameObject prefabToSpawn = null;
        CharacterBase characterEscolhido = null;

        List<CharacterBase> biblioteca = GameDataManager.Instance?.bibliotecaOriginalPersonagens;
        int charIndex = -1;

        if (ExoBeasts.Managers.GameModeManager.CurrentMode == ExoBeasts.Managers.GameMode.Multiplayer)
        {
            if (!CharacterChoiceCache.TryGet(clientId, out charIndex))
            {
                _spawnedClientIds.Remove(clientId);
                Debug.LogError($"[GameSetupManager] Spawn bloqueado: clientId={clientId} ainda nao registrou escolha autoritativa de comandante.");
                return;
            }
        }
        else if (!CharacterChoiceCache.TryGet(clientId, out charIndex))
        {
            charIndex = ResolveSingleplayerCharacterIndex(biblioteca);
        }

        if (biblioteca != null && charIndex >= 0 && charIndex < biblioteca.Count)
        {
            characterEscolhido = biblioteca[charIndex];
            prefabToSpawn = characterEscolhido?.commanderPrefab;
            if (prefabToSpawn == null) prefabToSpawn = characterEscolhido?.towerPrefab;
            Debug.Log($"[GameSetupManager] Spawn resolvido via CharacterChoiceCache: clientId={clientId}, charIndex={charIndex}, personagem={characterEscolhido?.name}");
        }

        // Fallback 1: Equipe Selecionada no GameDataManager
        if (prefabToSpawn == null && GameDataManager.Instance != null && GameDataManager.Instance.equipeSelecionada != null && GameDataManager.Instance.equipeSelecionada.Length > 0)
        {
            var comandanteEquipe = GameDataManager.Instance.equipeSelecionada[0];
            if (comandanteEquipe != null)
            {
                characterEscolhido = comandanteEquipe;
                prefabToSpawn = comandanteEquipe.commanderPrefab ?? comandanteEquipe.towerPrefab;
                Debug.Log($"[GameSetupManager] Prefab resolvido via GameDataManager.equipeSelecionada[0]: {comandanteEquipe.name}");
            }
        }

        // Fallback 2: Busca qualquer personagem com prefab válido na biblioteca ou em cena
        if (prefabToSpawn == null)
        {
            var todos = Resources.FindObjectsOfTypeAll<CharacterBase>();
            foreach (var c in todos)
            {
                if (c != null && (c.commanderPrefab != null || c.towerPrefab != null))
                {
                    characterEscolhido = c;
                    prefabToSpawn = c.commanderPrefab ?? c.towerPrefab;
                    Debug.LogWarning($"[GameSetupManager] Prefab resolvido via Fallback Global: {c.name}");
                    break;
                }
            }
        }

        if (prefabToSpawn == null)
        {
            _spawnedClientIds.Remove(clientId);
            Debug.LogError($"[GameSetupManager] Falha critica: nenhum commanderPrefab encontrado para clientId={clientId} (charIndex={charIndex}). Verifique CharacterChoiceCache e GameDataManager.bibliotecaOriginalPersonagens.");
            return;
        }

        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        int playerIndex = GetSpawnedPlayerCount();

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            Transform sp = spawnPoints[playerIndex % spawnPoints.Length];
            pos = sp.position;
            rot = sp.rotation;
        }
        else if (spawnPoint != null)
        {
            pos = spawnPoint.position;
            rot = spawnPoint.rotation;
        }

        // Instancia o boneco físico
        GameObject playerInstance = Instantiate(prefabToSpawn, pos, rot);
        EnsureRuntimePlayerNetworkContract(playerInstance);

        // =================================================================
        // A INJEÇÃO MÁGICA: Cola o characterData no boneco ANTES dele ligar!
        // =================================================================
        if (characterEscolhido != null)
        {
            if (playerInstance.TryGetComponent<PlayerCombatManager>(out var combatManager))
                combatManager.characterData = characterEscolhido;

            if (playerInstance.TryGetComponent<MeleeCombatSystem>(out var meleeCombat))
                meleeCombat.characterData = characterEscolhido;

            if (playerInstance.TryGetComponent<PlayerShooting>(out var shooting))
                shooting.characterData = characterEscolhido;

            if (playerInstance.TryGetComponent<PlayerHealthSystem>(out var health))
                health.characterData = characterEscolhido;

            if (playerInstance.TryGetComponent<CommanderAbilityController>(out var ability))
                ability.characterData = characterEscolhido;

            if (playerInstance.TryGetComponent<CommanderController>(out var commander))
                commander.characterData = characterEscolhido;
        }

        if (playerInstance.TryGetComponent<NetworkedPlayerController>(out var networkedPlayerController))
            networkedPlayerController.CharacterIndex.Value = charIndex;

        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnAsPlayerObject(clientId);
        }
        else
        {
            Debug.LogError(
                $"[GameSetupManager] Prefab '{prefabToSpawn.name}' nao tem NetworkObject! " +
                "Adicione o componente e registre o prefab em NetworkManager > NetworkPrefabsList. " +
                "Spawn local criado mas NAO e visivel para outros clientes.");
        }

        if (PlayerRegistry.Instance != null)
        {
            PlayerRegistry.Instance.RegisterPlayer(clientId, playerInstance, charIndex);
        }

        RefreshEnemyTargetsAfterPlayerRegistration(clientId);

        Debug.Log($"[GameSetupManager] Spawnou clientId={clientId} como charIndex={charIndex} ({characterEscolhido?.name})");

        if (BuildManager.Instance != null && GameDataManager.Instance != null)
            BuildManager.Instance.SetAvailableTowers(GameDataManager.Instance.equipeSelecionada);
    }

    private void RefreshEnemyTargetsAfterPlayerRegistration(ulong clientId)
    {
        if (!IsServer)
            return;

        EnemyController[] enemies = FindObjectsByType<EnemyController>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        foreach (EnemyController enemy in enemies)
        {
            if (enemy == null || enemy.IsDead)
                continue;

            enemy.RefreshTargetNow();
        }

        Debug.Log($"[GameSetupManager] Targets de {enemies.Length} inimigo(s) atualizados apos spawn/register clientId={clientId}.");
    }

    private bool HasExistingPlayerObject(ulong clientId)
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
            client.PlayerObject != null)
        {
            return PlayerRegistry.IsValidPlayerObject(client.PlayerObject.gameObject);
        }

        return PlayerRegistry.Instance != null && PlayerRegistry.Instance.GetPlayerObject(clientId) != null;
    }

    private int GetSpawnedPlayerCount()
    {
        PlayerRegistry.CollectValidPlayerTransforms(spawnedPlayerTargets);
        return spawnedPlayerTargets.Count;
    }

    private void ValidateObjectiveHealthSetup()
    {
        ObjectiveHealthSystem objectiveHealthSystem = GetComponent<ObjectiveHealthSystem>();
        if (objectiveHealthSystem == null)
        {
            Debug.LogError("[GameSetupManager] ObjectiveHealthSystem ausente em 'ManagersDaPartida'. Adicione o componente na cena para sincronizar a vida da base no multiplayer.");
            return;
        }

        if (ObjectiveHealthSystem.Instance != objectiveHealthSystem)
        {
            Debug.LogError("[GameSetupManager] Objetivo com instancia inconsistente. Verifique duplicatas de ObjectiveHealthSystem na cena.");
            return;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer && NetworkManager.Singleton != null)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.Singleton.OnClientDisconnectCallback -= OnClientDisconnected;
        }
        _spawnedClientIds.Clear();
        base.OnNetworkDespawn();
    }

    private int ResolveSingleplayerCharacterIndex(List<CharacterBase> biblioteca)
    {
        CharacterBase[] equipeSelecionada = GameDataManager.Instance?.equipeSelecionada;
        if (biblioteca == null || equipeSelecionada == null || equipeSelecionada.Length == 0)
            return 0;

        CharacterBase comandante = equipeSelecionada[0];
        if (comandante == null)
            return 0;

        string cleanName = comandante.name.Replace("(Clone)", "");
        int characterIndex = biblioteca.FindIndex(character => character != null && character.name == cleanName);
        return characterIndex >= 0 ? characterIndex : 0;
    }

    private void EnsureRuntimePlayerNetworkContract(GameObject playerInstance)
    {
        if (playerInstance == null)
            return;

        if (playerInstance.GetComponent<PlayerNetworkSetup>() == null)
        {
            playerInstance.AddComponent<PlayerNetworkSetup>();
            Debug.LogWarning($"[GameSetupManager] '{playerInstance.name}' recebeu PlayerNetworkSetup em runtime para padronizar o contrato multiplayer.");
        }

        if (playerInstance.GetComponent<LocalPlayerInputBridge>() == null &&
            playerInstance.GetComponent<UnityEngine.InputSystem.PlayerInput>() != null)
        {
            playerInstance.AddComponent<LocalPlayerInputBridge>();
        }

        if (playerInstance.GetComponent<Unity.Netcode.Components.NetworkTransform>() == null)
        {
            playerInstance.AddComponent<ClientNetworkTransform>();
            Debug.LogWarning($"[GameSetupManager] '{playerInstance.name}' recebeu ClientNetworkTransform em runtime.");
        }

        // REGRA DE OURO NGO: jogos com ClientNetworkTransform para player precisam de Rigidbody
        // Kinematic no prefab para que triggers Physics disparem no servidor com player remoto.
        // Sem isso: ClientNetworkTransform escreve transform.position direto → CharacterController
        // não atualiza Physics System (só Move() faz isso) → OnTriggerEnter NÃO dispara no servidor
        // para player remoto (Teleportador, Fogueira heal, Espinhos, Piche, Broca quebram).
        // Kinematic = não responde a forças (PlayerMovement não usa AddForce), mas Physics.SyncTransforms
        // automático quando transform muda → triggers disparam corretamente.
        if (playerInstance.GetComponent<Rigidbody>() == null)
        {
            Rigidbody rb = playerInstance.AddComponent<Rigidbody>();
            rb.isKinematic = true;
            rb.useGravity = false;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
            Debug.LogWarning($"[GameSetupManager] '{playerInstance.name}' recebeu Rigidbody Kinematic em runtime para detecção de triggers no servidor (player remoto).");
        }
    }
}
