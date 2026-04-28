using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class Teleportador : TrapLogicBase
{
    private static readonly List<Teleportador> portais = new List<Teleportador>();
    private const int MaxPortais = 2;

    private readonly HashSet<ulong> playersOnCooldown = new HashSet<ulong>();

    private Teleportador portalLigado;
    private float cooldownTeleporte = 1f;
    private float entradaOffset = 1.5f;
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

                if (antigo != null && antigo.TryGetComponent(out NetworkObject networkObject) && networkObject.IsSpawned)
                    networkObject.Despawn();
                else if (antigo != null)
                    Destroy(antigo.gameObject);
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
        if (!isServerMode || portalLigado == null || !other.CompareTag("Player"))
            return;

        if (!TryResolvePlayer(other, out NetworkObject playerObject, out GameObject localPlayerObject, out ulong playerKey))
            return;

        if (playersOnCooldown.Contains(playerKey) || portalLigado.playersOnCooldown.Contains(playerKey))
            return;

        Vector3 destination = portalLigado.transform.position + (portalLigado.transform.forward * entradaOffset);
        Quaternion destinationRotation = Quaternion.LookRotation(portalLigado.transform.forward, Vector3.up);

        if (playerObject != null && playerObject.IsSpawned)
            PlayerTeleportService.TeleportServerValidated(playerObject, destination, destinationRotation);
        else
            PlayerTeleportService.TeleportLocal(localPlayerObject, destination, destinationRotation);

        StartCoroutine(StartCooldown(playerKey));
        StartCoroutine(portalLigado.StartCooldown(playerKey));
    }

    private IEnumerator StartCooldown(ulong playerKey)
    {
        playersOnCooldown.Add(playerKey);
        yield return new WaitForSeconds(cooldownTeleporte);
        playersOnCooldown.Remove(playerKey);
    }

    private bool TryResolvePlayer(Collider other, out NetworkObject playerObject, out GameObject localPlayerObject, out ulong playerKey)
    {
        playerObject = other.GetComponent<NetworkObject>();
        localPlayerObject = other.gameObject;

        if (playerObject != null)
        {
            playerKey = playerObject.NetworkObjectId;
            return true;
        }

        playerKey = 0;
        return localPlayerObject != null;
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
