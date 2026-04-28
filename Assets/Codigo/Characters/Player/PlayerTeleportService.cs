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

        ClientNetworkTransform networkTransform = player.GetComponent<ClientNetworkTransform>();
        if (networkTransform != null && player.IsSpawned)
            networkTransform.SetState(position, rotation, player.transform.localScale, teleportDisabled: false);

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
    }
}
