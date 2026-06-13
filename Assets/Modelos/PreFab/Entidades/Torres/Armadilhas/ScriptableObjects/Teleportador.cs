using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Teleportador : TrapLogicBase
{
    private static readonly List<Teleportador> portais = new List<Teleportador>();
    private const int MaxPortais = 2;

    private static readonly Dictionary<ulong, float> unpairedNotificationCooldowns = new Dictionary<ulong, float>();
    private const float UnpairedNotificationCooldownSeconds = 3f;

    private readonly HashSet<ulong> playersOnCooldown = new HashSet<ulong>();

    private Teleportador portalLigado;
    [SerializeField] private float cooldownTeleporte = 1f;
    [SerializeField] private float entradaOffset = 1.5f;
    [SerializeField] private bool debugTeleportLogs;

    private bool isServerMode;
    private bool isSetup;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isServerMode = IsServer;
        SetupPortal();
    }

    private void Start()
    {
        isServerMode = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        SetupPortal();
    }

    private new void OnDestroy()
    {
        portais.Remove(this);
        if (portalLigado != null)
            portalLigado.portalLigado = null;

        LigarPortais();
    }

    public static int GetPortalCount() => portais.Count;

    private void SetupPortal()
    {
        if (isSetup)
            return;

        isSetup = true;
        GetComponent<Collider>().isTrigger = true;

        portais.RemoveAll(portal => portal == null);

        if (portais.Count >= MaxPortais)
        {
            if (isServerMode)
            {
                Teleportador antigo = portais[0];
                portais.RemoveAt(0);

                if (antigo != null)
                    antigo.DestroyTrapServer(false);
            }
            else
            {
                return;
            }
        }

        portais.Add(this);
        LigarPortais();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServerMode)
            return;

        if (!TryResolvePlayer(other, out NetworkObject playerObject, out GameObject localPlayerObject, out ulong playerKey))
        {
            if (LooksLikePlayerCollider(other))
                LogTeleportWarning($"Trigger recebeu um possivel player, mas nao conseguiu resolver um NetworkObject valido. Collider: {GetColliderPath(other)}");
            else
                LogTeleportDebug($"Trigger ignorado por nao ser player. Collider: {GetColliderPath(other)}");

            return;
        }

        // Portal solo: notifica o player owner (amarelo) com cooldown de 3s para evitar spam.
        if (portalLigado == null)
        {
            NotifyUnpairedPortalToOwner(playerObject);
            return;
        }

        if (playersOnCooldown.Contains(playerKey) || portalLigado.playersOnCooldown.Contains(playerKey))
            return;

        Vector3 destination = portalLigado.transform.position + (portalLigado.transform.forward * entradaOffset);
        Quaternion destinationRotation = Quaternion.LookRotation(portalLigado.transform.forward, Vector3.up);

        if (playerObject != null && playerObject.IsSpawned)
        {
            LogTeleportDebug($"Teleportando player {playerKey} para {destination}.");
            PlayerTeleportService.TeleportServerValidated(playerObject, destination, destinationRotation);

            if (playerObject.OwnerClientId != NetworkManager.ServerClientId)
            {
                ClientRpcParams targetParams = new ClientRpcParams
                {
                    Send = new ClientRpcSendParams
                    {
                        TargetClientIds = new ulong[] { playerObject.OwnerClientId }
                    }
                };
                NotifyPlayerTeleportClientRpc(
                    playerObject.NetworkObjectId,
                    destination,
                    destinationRotation,
                    targetParams);
            }
        }
        else
        {
            LogTeleportDebug($"Teleportando player local sem NetworkObject para {destination}.");
            PlayerTeleportService.TeleportLocal(localPlayerObject, destination, destinationRotation);
        }

        StartCoroutine(StartCooldown(playerKey));
        StartCoroutine(portalLigado.StartCooldown(playerKey));
    }

    [ClientRpc]
    private void NotifyPlayerTeleportClientRpc(
        ulong playerNetObjId,
        Vector3 destination,
        Quaternion rotation,
        ClientRpcParams rpcParams = default)
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        if (networkManager == null || networkManager.SpawnManager == null)
        {
            LogTeleportWarning($"Cliente recebeu teleporte para NetworkObject {playerNetObjId}, mas nao ha NetworkManager/SpawnManager disponivel.");
            return;
        }

        if (!networkManager.SpawnManager.SpawnedObjects.TryGetValue(playerNetObjId, out NetworkObject playerObj))
        {
            LogTeleportWarning($"Cliente recebeu teleporte para NetworkObject {playerNetObjId}, mas o objeto nao esta em SpawnedObjects.");
            return;
        }

        if (!playerObj.IsOwner)
            return;

        PlayerTeleportService.TeleportLocal(playerObj.gameObject, destination, rotation);
    }

    private IEnumerator StartCooldown(ulong playerKey)
    {
        playersOnCooldown.Add(playerKey);
        yield return new WaitForSeconds(cooldownTeleporte);
        playersOnCooldown.Remove(playerKey);
    }

    private void NotifyUnpairedPortalToOwner(NetworkObject playerObject)
    {
        if (playerObject == null)
            return;

        ulong ownerId = playerObject.OwnerClientId;
        float now = Time.unscaledTime;
        if (unpairedNotificationCooldowns.TryGetValue(ownerId, out float lastNotify) &&
            now - lastNotify < UnpairedNotificationCooldownSeconds)
        {
            return;
        }
        unpairedNotificationCooldowns[ownerId] = now;

        if (NetworkManager.Singleton != null && ownerId == NetworkManager.ServerClientId)
        {
            ShowUnpairedNotificationLocal();
            return;
        }

        ClientRpcParams targetParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { ownerId }
            }
        };
        NotifyUnpairedPortalClientRpc(targetParams);
    }

    [ClientRpc]
    private void NotifyUnpairedPortalClientRpc(ClientRpcParams rpcParams = default)
    {
        ShowUnpairedNotificationLocal();
    }

    private static void ShowUnpairedNotificationLocal()
    {
        if (UINotificationManager.Instance != null)
        {
            UINotificationManager.Instance.ShowLocalNotification(
                "Coloque um segundo portal para teleportar!",
                new Color(1f, 0.85f, 0.2f));
        }
    }

    private bool TryResolvePlayer(Collider other, out NetworkObject playerObject, out GameObject localPlayerObject, out ulong playerKey)
    {
        playerObject = null;
        localPlayerObject = null;
        playerKey = 0;

        if (other == null)
            return false;

        GameObject playerRoot = ResolvePlayerRoot(other);
        NetworkObject resolvedNetworkObject = ResolvePlayerNetworkObject(other, playerRoot);
        bool networkSessionActive = NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening;
        bool resolvedAsPlayer = playerRoot != null || (resolvedNetworkObject != null && IsPlayerObject(resolvedNetworkObject.gameObject));

        if (!resolvedAsPlayer)
            return false;

        if (networkSessionActive)
        {
            if (resolvedNetworkObject == null || !resolvedNetworkObject.IsSpawned)
                return false;

            playerObject = resolvedNetworkObject;
            localPlayerObject = playerRoot != null ? playerRoot : resolvedNetworkObject.gameObject;
            playerKey = resolvedNetworkObject.NetworkObjectId;
            return true;
        }

        if (playerRoot == null && resolvedNetworkObject != null && IsPlayerObject(resolvedNetworkObject.gameObject))
            playerRoot = resolvedNetworkObject.gameObject;

        if (playerRoot == null)
            return false;

        playerObject = resolvedNetworkObject;
        localPlayerObject = playerRoot;
        playerKey = resolvedNetworkObject != null ? resolvedNetworkObject.NetworkObjectId : 0;
        return true;
    }

    private static GameObject ResolvePlayerRoot(Collider other)
    {
        PlayerMovement movement = other.GetComponentInParent<PlayerMovement>();
        if (movement != null)
            return movement.gameObject;

        PlayerHealthSystem healthSystem = other.GetComponentInParent<PlayerHealthSystem>();
        if (healthSystem != null)
            return healthSystem.gameObject;

        Transform current = other.transform;
        while (current != null)
        {
            if (current.CompareTag("Player"))
                return current.gameObject;

            current = current.parent;
        }

        return null;
    }

    private static NetworkObject ResolvePlayerNetworkObject(Collider other, GameObject playerRoot)
    {
        NetworkObject networkObject = null;

        if (playerRoot != null)
            networkObject = playerRoot.GetComponent<NetworkObject>();

        if (networkObject == null)
            networkObject = other.GetComponentInParent<NetworkObject>();

        if (networkObject == null || IsPlayerObject(networkObject.gameObject))
            return networkObject;

        NetworkObject parentNetworkObject = networkObject.transform.parent != null
            ? networkObject.transform.parent.GetComponentInParent<NetworkObject>()
            : null;

        return parentNetworkObject != null && IsPlayerObject(parentNetworkObject.gameObject)
            ? parentNetworkObject
            : networkObject;
    }

    private static bool IsPlayerObject(GameObject gameObject)
    {
        if (gameObject == null)
            return false;

        return gameObject.CompareTag("Player")
            || gameObject.GetComponent<PlayerMovement>() != null
            || gameObject.GetComponent<PlayerHealthSystem>() != null;
    }

    private static bool LooksLikePlayerCollider(Collider other)
    {
        if (other == null)
            return false;

        if (other.CompareTag("Player"))
            return true;

        if (other.GetComponentInParent<PlayerMovement>() != null || other.GetComponentInParent<PlayerHealthSystem>() != null)
            return true;

        NetworkObject networkObject = other.GetComponentInParent<NetworkObject>();
        return networkObject != null && IsPlayerObject(networkObject.gameObject);
    }

    private void LogTeleportWarning(string message)
    {
        Debug.LogWarning($"[{nameof(Teleportador)}] {message}", this);
    }

    private void LogTeleportDebug(string message)
    {
        if (debugTeleportLogs)
            Debug.Log($"[{nameof(Teleportador)}] {message}", this);
    }

    private static string GetColliderPath(Collider collider)
    {
        if (collider == null)
            return "<null>";

        Transform current = collider.transform;
        string path = current.name;

        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        return path;
    }

    private static void LigarPortais()
    {
        if (portais.Count == 1)
        {
            portais[0].portalLigado = null;
        }
        else if (portais.Count >= 2)
        {
            portais[0].portalLigado = portais[1];
            portais[1].portalLigado = portais[0];
        }
    }
}
