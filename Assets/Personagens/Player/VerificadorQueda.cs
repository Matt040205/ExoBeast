using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── VerificadorQueda ────────────────────────────────────
/// Detecta queda do mapa (y < limiteY) e teleporta o jogador para o SpawnPoint.
///
///  ▸ Owner-only: desativado nos remotos em OnNetworkSpawn
///  ▸ Usa GameSetupManager.Instance para obter o SpawnPoint (sem FindObjectOfType)
///  ▸ ClientNetworkTransform replica a nova posicao para servidor e demais clientes
/// ─────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class VerificadorQueda : NetworkBehaviour
{
    [Header("Configuracao de Queda")]
    [Tooltip("A altura Y em que o jogador sera teleportado de volta.")]
    public float limiteY = -30f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Apenas o dono do objeto detecta e controla a queda (Owner-auth movement)
        if (!IsOwner)
        {
            this.enabled = false;
            return;
        }
    }

    void Update()
    {
        // Ja garantido pelo OnNetworkSpawn que apenas IsOwner processa Update
        if (transform.position.y < limiteY)
        {
            TeleportarParaSpawn();
        }
    }

    void TeleportarParaSpawn()
    {
        if (!GameSetupManager.TryResolveRespawnPose("RespawnPoint", out Vector3 targetPos, out Quaternion targetRot))
        {
            Debug.LogError($"[VerificadorQueda] Nenhum respawn valido encontrado para '{name}'. Teleporte de queda cancelado para evitar Vector3.zero.");
            return;
        }

        PlayerTeleportService.TeleportLocal(gameObject, targetPos, targetRot);

        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening && IsSpawned)
            RequestFallRecoveryServerRpc(targetPos, targetRot);
    }

    [ServerRpc]
    private void RequestFallRecoveryServerRpc(Vector3 ownerResolvedPosition, Quaternion ownerResolvedRotation)
    {
        if (NetworkObject == null || !NetworkObject.IsSpawned)
            return;

        Vector3 targetPos = ownerResolvedPosition;
        Quaternion targetRot = ownerResolvedRotation;

        if (!GameSetupManager.TryResolveRespawnPose("RespawnPoint", out targetPos, out targetRot))
        {
            Debug.LogWarning($"[VerificadorQueda] Servidor nao encontrou respawn; usando pose resolvida pelo owner para '{name}'.");
        }

        PlayerTeleportService.TeleportServerValidated(NetworkObject, targetPos, targetRot);

        if (OwnerClientId == NetworkManager.ServerClientId)
            return;

        ClientRpcParams targetParams = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new ulong[] { OwnerClientId }
            }
        };
        ConfirmFallRecoveryClientRpc(targetPos, targetRot, targetParams);
    }

    [ClientRpc]
    private void ConfirmFallRecoveryClientRpc(Vector3 serverPosition, Quaternion serverRotation, ClientRpcParams rpcParams = default)
    {
        if (!IsOwner)
            return;

        PlayerTeleportService.TeleportLocal(gameObject, serverPosition, serverRotation);
    }
}
