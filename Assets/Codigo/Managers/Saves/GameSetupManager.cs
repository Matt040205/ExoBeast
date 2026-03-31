using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using ExoBeasts.Multiplayer.GameServer;

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
            SpawnPlayerServerSide(NetworkManager.Singleton.LocalClientId);
            NetworkManager.Singleton.OnClientConnectedCallback += OnClientConnected;
        }
    }

    private void OnClientConnected(ulong clientId)
    {
        if (!IsServer) return;
        SpawnPlayerServerSide(clientId);
    }

    private void SpawnPlayerServerSide(ulong clientId)
    {
        if (!IsServer) return;

        if (PlayerRegistry.Instance != null && PlayerRegistry.Instance.GetPlayerObject(clientId) != null)
            return;

        GameObject prefabToSpawn = null;
        CharacterBase characterEscolhido = null; // <--- Guarda o cartão de dados selecionado

        // 1. Pega os dados exatos do Singleton
        if (PlayerRegistry.Instance != null && GameDataManager.Instance != null)
        {
            int charIndex = PlayerRegistry.Instance.GetPlayerCharacterChoice(clientId);

            if (charIndex >= 0 && charIndex < GameDataManager.Instance.bibliotecaOriginalPersonagens.Count)
            {
                characterEscolhido = GameDataManager.Instance.bibliotecaOriginalPersonagens[charIndex];
                prefabToSpawn = characterEscolhido.commanderPrefab;
            }
        }

        // 2. Fallback de Segurança
        if (prefabToSpawn == null && GameDataManager.Instance != null && GameDataManager.Instance.equipeSelecionada[0] != null)
        {
            characterEscolhido = GameDataManager.Instance.equipeSelecionada[0];
            prefabToSpawn = characterEscolhido.commanderPrefab;
        }

        if (prefabToSpawn == null)
        {
            Debug.LogError("[GameSetupManager] Falha crítica: Nenhum 'commanderPrefab' encontrado.");
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

        if (PlayerRegistry.Instance != null)
        {
            PlayerRegistry.Instance.RegisterPlayer(clientId, playerInstance, 0);
        }

        if (BuildManager.Instance != null && GameDataManager.Instance != null)
        {
            BuildManager.Instance.SetAvailableTowers(GameDataManager.Instance.equipeSelecionada);
        }
    }

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            NetworkManager.Singleton.OnClientConnectedCallback -= OnClientConnected;
        }
        base.OnNetworkDespawn();
    }
}