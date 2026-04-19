using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using ExoBeasts.Multiplayer.GameServer;
using ExoBeasts.Multiplayer.Core;

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
            SpawnPlayerServerSide(NetworkManager.Singleton.LocalClientId);
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

        if (PlayerRegistry.Instance != null && PlayerRegistry.Instance.GetPlayerObject(clientId) != null)
            return;

        GameObject prefabToSpawn = null;
        CharacterBase characterEscolhido = null; // <--- Guarda o cartão de dados selecionado

        // 1. Pega os dados exatos do Singleton
        // Fonte primaria: CharacterChoiceCache (populado pelo LobbyManager via ConnectionApproval).
        // PlayerRegistry e mantido como espelho para codigo legado que le de la.
        int charIndex = CharacterChoiceCache.Get(clientId, fallback: 0);

        if (GameDataManager.Instance == null)
        {
            _spawnedClientIds.Remove(clientId);
            Debug.LogError("[GameSetupManager] GameDataManager.Instance nulo — scene setup incompleto. Abortando spawn.");
            return;
        }

        var biblioteca = GameDataManager.Instance.bibliotecaOriginalPersonagens;
        if (biblioteca != null && charIndex >= 0 && charIndex < biblioteca.Count)
        {
            characterEscolhido = biblioteca[charIndex];
            prefabToSpawn = characterEscolhido?.commanderPrefab;
        }
        else
        {
            Debug.LogWarning($"[GameSetupManager] charIndex={charIndex} fora de range (biblioteca.Count={biblioteca?.Count ?? 0}). Tentando fallback equipeSelecionada[0].");
        }

        // 2. Fallback de Segurança — com bounds check (C6 audit).
        var equipe = GameDataManager.Instance.equipeSelecionada;
        if (prefabToSpawn == null && equipe != null && equipe.Length > 0 && equipe[0] != null)
        {
            characterEscolhido = equipe[0];
            prefabToSpawn = characterEscolhido.commanderPrefab;
        }

        if (prefabToSpawn == null)
        {
            _spawnedClientIds.Remove(clientId);
            Debug.LogError($"[GameSetupManager] Falha crítica: nenhum 'commanderPrefab' encontrado para clientId={clientId} (charIndex={charIndex}). Configure GameDataManager.bibliotecaOriginalPersonagens ou equipeSelecionada[0] no Inspector.");
            return;
        }

        Vector3 pos = Vector3.zero;
        Quaternion rot = Quaternion.identity;
        int playerIndex = (PlayerRegistry.Instance != null) ? PlayerRegistry.Instance.GetAllPlayers().Count : 0;

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

        // =================================================================
        // A INJEÇÃO MÁGICA: Cola o characterData no boneco ANTES dele ligar!
        // =================================================================
        if (characterEscolhido != null)
        {
            if (playerInstance.TryGetComponent<PlayerShooting>(out var shooting))
                shooting.characterData = characterEscolhido;

            if (playerInstance.TryGetComponent<PlayerHealthSystem>(out var health))
                health.characterData = characterEscolhido;

            if (playerInstance.TryGetComponent<CommanderAbilityController>(out var ability))
                ability.characterData = characterEscolhido;
        }

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

        Debug.Log($"[GameSetupManager] Spawnou clientId={clientId} como charIndex={charIndex} ({characterEscolhido?.name})");

        if (BuildManager.Instance != null && GameDataManager.Instance != null)
        {
            BuildManager.Instance.SetAvailableTowers(GameDataManager.Instance.equipeSelecionada);
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
}