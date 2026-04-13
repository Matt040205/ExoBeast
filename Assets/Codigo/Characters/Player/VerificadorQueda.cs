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

    private Transform spawnPoint;
    private CharacterController controller;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Apenas o dono do objeto detecta e controla a queda (Owner-auth movement)
        if (!IsOwner)
        {
            this.enabled = false;
            return;
        }

        controller = GetComponent<CharacterController>();

        if (GameSetupManager.Instance != null && GameSetupManager.Instance.spawnPoint != null)
        {
            spawnPoint = GameSetupManager.Instance.spawnPoint;
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
        Vector3 targetPos = (spawnPoint != null) ? spawnPoint.position : Vector3.zero;
        Quaternion targetRot = (spawnPoint != null) ? spawnPoint.rotation : Quaternion.identity;

        // Desativa o controller para permitir mudanca direta de transform
        controller.enabled = false;
        transform.position = targetPos;
        transform.rotation = targetRot;
        controller.enabled = true;

        // O ClientNetworkTransform replicara esta nova posicao para o servidor e demais clientes
    }
}
