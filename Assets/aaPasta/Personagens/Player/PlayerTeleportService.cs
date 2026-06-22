using Unity.Netcode;
using UnityEngine;
using ExoBeasts.Multiplayer.Sync;

public static class PlayerTeleportService
{
    public static bool TeleportServerValidated(NetworkObject player, Vector3 position, Quaternion rotation)
    {
        if (player == null)
            return false;

        TeleportInternal(player.gameObject, position, rotation);

        // REGRA DE OURO NGO: SetState() em ClientNetworkTransform server-side é IGNORADO para
        // players não-host owners (CNT é owner-authoritative). Só faz sentido quando o servidor
        // É o owner — caso do host-owned player. Para players remotos, o caller (Teleportador.cs)
        // envia NotifyPlayerTeleportClientRpc owner-targeted que faz TeleportLocal no cliente owner.
        ClientNetworkTransform networkTransform = player.GetComponent<ClientNetworkTransform>();
        if (networkTransform != null && player.IsSpawned &&
            player.OwnerClientId == NetworkManager.ServerClientId)
        {
            networkTransform.SetState(position, rotation, player.transform.localScale, teleportDisabled: false);
        }

        return true;
    }

    public static void TeleportLocal(GameObject player, Vector3 position, Quaternion rotation)
    {
        if (player == null)
            return;

        TeleportInternal(player, position, rotation);
    }

    private static void TeleportInternal(GameObject player, Vector3 position, Quaternion rotation)
    {
        CharacterController characterController = player.GetComponent<CharacterController>();
        bool wasEnabled = characterController != null && characterController.enabled;

        if (wasEnabled)
            characterController.enabled = false;

        player.transform.SetPositionAndRotation(position, rotation);
        Physics.SyncTransforms();

        if (wasEnabled)
            characterController.enabled = true;

        PlayerMovement movement = player.GetComponent<PlayerMovement>();
        if (movement != null)
            movement.ResetMotionAfterTeleport();
    }
}
