using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;

/// <summary>
/// Teleportador: Transporta jogadores entre dois portais.
/// O logicPrefab deve ter NetworkObject para que ClientRpc funcione no multiplayer.
/// No modo offline/host, teleporta diretamente ao detectar o trigger.
/// </summary>
[RequireComponent(typeof(BoxCollider))]
public class Teleportador : TrapLogicBase
{
    private static List<Teleportador> portais = new List<Teleportador>();
    private const int MAX_PORTAIS = 2;

    private Teleportador portalLigado;
    private bool podeTeleportar = true;
    private float cooldownTeleporte = 1f;
    private float entradaOffset = 1.5f;
    private bool isServerMode = false;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        isServerMode = IsServer;
        SetupPortal();
    }

    void Start()
    {
        // Funciona offline E como host (IsServer = true em ambos)
        isServerMode = NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;
        SetupPortal();
    }

    private void SetupPortal()
    {
        GetComponent<Collider>().isTrigger = true;

        if (portais.Count >= MAX_PORTAIS)
        {
            if (isServerMode)
            {
                Teleportador antigo = portais[0];
                portais.RemoveAt(0);
                if (antigo != null && antigo.TryGetComponent<NetworkObject>(out var no) && no.IsSpawned)
                    no.Despawn();
                else if (antigo != null)
                    Destroy(antigo.gameObject);
            }
            else return;
        }

        portais.Add(this);
        LigarPortais();
    }

    void OnDestroy()
    {
        portais.Remove(this);
        if (portalLigado != null) portalLigado.portalLigado = null;
        LigarPortais();
    }

    public static int GetPortalCount() => portais.Count;

    private static void LigarPortais()
    {
        if (portais.Count == 1) portais[0].portalLigado = null;
        else if (portais.Count >= 2)
        {
            portais[0].portalLigado = portais[1];
            portais[1].portalLigado = portais[0];
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (!isServerMode) return;
        if (!podeTeleportar || portalLigado == null) return;
        if (!other.CompareTag("Player")) return;

        Vector3 destino = portalLigado.transform.position + (portalLigado.transform.forward * entradaOffset);

        // Tenta usar ClientRpc se este objeto estiver spawnado na rede
        NetworkObject meuNetObj = GetComponent<NetworkObject>();
        if (meuNetObj != null && meuNetObj.IsSpawned)
        {
            NetworkObject jogadorNet = other.GetComponent<NetworkObject>();
            if (jogadorNet != null)
                TeleportarJogadorClientRpc(jogadorNet.NetworkObjectId, destino);
        }
        else
        {
            // Modo offline: teleporta localmente
            TeleportarLocal(other.gameObject, destino);
        }

        StartCoroutine(IniciarCooldown());
        StartCoroutine(portalLigado.IniciarCooldown());
    }

    [ClientRpc]
    private void TeleportarJogadorClientRpc(ulong jogadorNetId, Vector3 destino)
    {
        if (NetworkManager.Singleton != null &&
            NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(jogadorNetId, out var netObj))
        {
            TeleportarLocal(netObj.gameObject, destino);
        }
    }

    private void TeleportarLocal(GameObject jogador, Vector3 destino)
    {
        CharacterController cc = jogador.GetComponent<CharacterController>();
        if (cc != null)
        {
            cc.enabled = false;
            jogador.transform.position = destino;
            cc.enabled = true;
        }
        else
        {
            jogador.transform.position = destino;
        }
    }

    private IEnumerator IniciarCooldown()
    {
        podeTeleportar = false;
        yield return new WaitForSeconds(cooldownTeleporte);
        podeTeleportar = true;
    }
}