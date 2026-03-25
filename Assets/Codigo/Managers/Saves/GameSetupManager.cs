using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using ExoBeasts.Multiplayer.GameServer;

/// <summary>
/// ── GameSetupManager ───────────────────────────────────
/// Responsavel por spawnar o prefab do jogador quando um cliente conecta.
///
///  ▸ OnNetworkSpawn: Host spawna a si mesmo; escuta novos clientes
///  ▸ SpawnPlayerServerSide: instancia prefab, SpawnAsPlayerObject, registra no PlayerRegistry
///  ▸ Usa spawnPoints[] para evitar sobreposicao de jogadores
///  ▸ Le personagem do PlayerRegistry (Connection Approval Payload em multiplayer)
/// ─────────────────────────────────────────────────────
/// </summary>
public class GameSetupManager : NetworkBehaviour
{
    public static GameSetupManager Instance { get; private set; }

    [Header("Spawn Configs")]
    public GameObject[] characterPrefabs;
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

        int characterIndex = 0;
        if (PlayerRegistry.Instance != null)
        {
            characterIndex = PlayerRegistry.Instance.GetPlayerCharacterChoice(clientId);
        }

        characterIndex = Mathf.Clamp(characterIndex, 0, characterPrefabs.Length - 1);
        GameObject prefabToSpawn = characterPrefabs[characterIndex];

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

        GameObject playerInstance = Instantiate(prefabToSpawn, pos, rot);
        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();
        if (netObj != null)
        {
            netObj.SpawnAsPlayerObject(clientId);
        }

        if (PlayerRegistry.Instance != null)
        {
            PlayerRegistry.Instance.RegisterPlayer(clientId, playerInstance, characterIndex);
        }

        // TODO: Em multiplayer, cada jogador deveria ter sua propria selecao de torres
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
