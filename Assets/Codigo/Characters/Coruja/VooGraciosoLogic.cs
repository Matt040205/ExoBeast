using UnityEngine;
using Unity.Netcode;
using System.Collections;

/// <summary>
/// ── VooGraciosoLogic ─────────────────────────────────────
/// Spawned NetworkObject that grants improved jump height and a bonus shot while airborne.
///
///  ▸ Owner applies local movement modifiers (jump, float) for immediate responsiveness
///  ▸ Server applies SetNextShotBonus so damage is authoritative
///  ▸ Despawn is requested when the owner lands; OnNetworkDespawn resets movement state
/// ─────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class VooGraciosoLogic : NetworkBehaviour
{
    private NetworkVariable<NetworkObjectReference> netOwner = new NetworkVariable<NetworkObjectReference>();
    private NetworkVariable<float> netJumpHeightModifier = new NetworkVariable<float>();
    private NetworkVariable<float> netStaticAimDuration = new NetworkVariable<float>();
    private NetworkVariable<float> netBonusDamage = new NetworkVariable<float>();
    private NetworkVariable<float> netBonusRadius = new NetworkVariable<float>();

    private GameObject ownerObject;
    private PlayerMovement playerMovement;
    private PlayerShooting playerShooting;
    private CommanderAbilityController abilityController;
    private Ability sourceAbility;
    private bool isActive = false;

    public void StartEffect(GameObject quemUsou, float jumpHeightModifier, float staticAimDuration, float bonusDamage, float bonusRadius, CommanderAbilityController controller, Ability ability)
    {
        if (!IsServer) return;

        netOwner.Value = new NetworkObjectReference(quemUsou.GetComponent<NetworkObject>());
        netJumpHeightModifier.Value = jumpHeightModifier;
        netStaticAimDuration.Value = staticAimDuration;
        netBonusDamage.Value = bonusDamage;
        netBonusRadius.Value = bonusRadius;

        var serverShooting = quemUsou.GetComponent<PlayerShooting>();
        if (serverShooting != null)
        {
            serverShooting.SetNextShotBonus(bonusDamage, bonusRadius);
        }

        isActive = true;
    }

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (netOwner.Value.TryGet(out NetworkObject ownerNO))
        {
            ownerObject = ownerNO.gameObject;
            playerMovement = ownerObject.GetComponent<PlayerMovement>();
            playerShooting = ownerObject.GetComponent<PlayerShooting>();
            abilityController = ownerObject.GetComponent<CommanderAbilityController>();

            // ownerNO.IsOwner = verdadeiro na maquina DO JOGADOR que usou a habilidade
            // (nao confundir com IsOwner deste NetworkObject, que e sempre o servidor)
            if (ownerNO.IsOwner)
            {
                if (playerMovement != null)
                {
                    playerMovement.jumpHeightModifier = netJumpHeightModifier.Value;

                    if (!playerMovement.isGrounded)
                    {
                        playerMovement.isFloating = true;
                        playerMovement.floatDuration = netStaticAimDuration.Value;
                    }
                }
            }
        }

        isActive = true;
    }

    private void Update()
    {
        if (!isActive || playerMovement == null) return;

        // Checa se ESTE cliente e o dono do jogador (nao do helper object)
        bool isPlayerOwner = netOwner.Value.TryGet(out NetworkObject ownerNO) && ownerNO.IsOwner;

        if (isPlayerOwner && playerMovement.isGrounded)
        {
            RequestDestroyServerRpc();
        }

        // Servidor tambem monitora o pouso para garantir cleanup mesmo se o RPC falhar
        if (IsServer && playerMovement.isGrounded)
        {
            DestroyLogic();
        }
    }

    [ServerRpc]
    private void RequestDestroyServerRpc()
    {
        DestroyLogic();
    }

    private void DestroyLogic()
    {
        if (!IsServer) return;

        // jumpHeightModifier reset is handled client-side in OnNetworkDespawn
        if (NetworkObject.IsSpawned)
            NetworkObject.Despawn();
    }

    public override void OnNetworkDespawn()
    {
        // Resetar modificadores na maquina do jogador dono (nao do helper object)
        if (netOwner.Value.TryGet(out NetworkObject ownerNO) && ownerNO.IsOwner && playerMovement != null)
        {
            playerMovement.jumpHeightModifier = 1f;
            playerMovement.isFloating = false;
        }
        base.OnNetworkDespawn();
    }
}
