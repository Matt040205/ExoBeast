using UnityEngine;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;
using ExoBeasts.Multiplayer.GameServer;

/// <summary>
/// ── GameSetupManager ───────────────────────────────────
/// Responsavel por spawnar o prefab do jogador quando um cliente conecta.
///
///  ▸ Pesca o prefab diretamente do GameDataManager via ScriptableObject (commanderPrefab).
///  ▸ OnNetworkSpawn: Host spawna a si mesmo; escuta novos clientes.
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

        // Se o jogador já existe, não spawna de novo
        if (PlayerRegistry.Instance != null && PlayerRegistry.Instance.GetPlayerObject(clientId) != null)
            return;

        GameObject prefabToSpawn = null;

        // 1. Tenta pegar a escolha exata do jogador via PlayerRegistry e GameDataManager
        if (PlayerRegistry.Instance != null && GameDataManager.Instance != null)
        {
            int charIndex = PlayerRegistry.Instance.GetPlayerCharacterChoice(clientId);

            if (charIndex >= 0 && charIndex < GameDataManager.Instance.bibliotecaOriginalPersonagens.Count)
            {
                CharacterBase baseChar = GameDataManager.Instance.bibliotecaOriginalPersonagens[charIndex];

                // Puxando o commanderPrefab da sua script CharacterBase!
                prefabToSpawn = baseChar.commanderPrefab;
            }
        }

        // 2. Fallback de Segurança: Se não achou pelo Registry, pega o Comandante do Slot 0 da Equipe Local
        if (prefabToSpawn == null && GameDataManager.Instance != null && GameDataManager.Instance.equipeSelecionada[0] != null)
        {
            prefabToSpawn = GameDataManager.Instance.equipeSelecionada[0].commanderPrefab;
        }

        // Se chegou aqui e continua nulo, o jogo avisa o que você esqueceu no Unity
        if (prefabToSpawn == null)
        {
            Debug.LogError("[GameSetupManager] Falha crítica: Nenhum 'commanderPrefab' foi encontrado no GameDataManager. Verifique se o seu CharacterBase selecionado tem um prefab associado nele!");
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

        // Instancia o boneco do jogador
        GameObject playerInstance = Instantiate(prefabToSpawn, pos, rot);
        NetworkObject netObj = playerInstance.GetComponent<NetworkObject>();

        if (netObj != null)
        {
            // Autoriza o jogador a controlar esse boneco na rede
            netObj.SpawnAsPlayerObject(clientId);
        }
        else
        {
            Debug.LogError("[GameSetupManager] O seu 'commanderPrefab' NÃO possui um componente NetworkObject! Ele precisa ter um para andar no Multiplayer.");
        }

        if (PlayerRegistry.Instance != null)
        {
            PlayerRegistry.Instance.RegisterPlayer(clientId, playerInstance, 0);
        }

        // Passa as torres escolhidas para o BuildManager (que você já tinha feito brilhantemente)
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