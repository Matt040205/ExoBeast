using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── VooGraciosoLogic ─────────────────────────────────────
/// Componente persistente no prefab do player que gerencia o estado
/// de "voo gracioso" (Q da Coruja).
///
///  ▸ Server: recebe StartEffect(), seta parâmetros e netIsActive = true
///  ▸ Owner (via OnValueChanged): aplica jumpHeightModifier, floating e bonus de tiro
///  ▸ Owner/Server monitoram pouso em Update() para resetar netIsActive = false
/// ─────────────────────────────────────────────────────────
/// </summary>
[RequireComponent(typeof(NetworkObject))]
public class VooGraciosoLogic : NetworkBehaviour
{
    private NetworkVariable<bool> netIsActive = new NetworkVariable<bool>(
        false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private NetworkVariable<float> netJumpHeightModifier = new NetworkVariable<float>();
    private NetworkVariable<float> netStaticAimDuration = new NetworkVariable<float>();
    private NetworkVariable<float> netBonusDamage = new NetworkVariable<float>();
    private NetworkVariable<float> netBonusRadius = new NetworkVariable<float>();

    private PlayerMovement playerMovement;
    private PlayerShooting playerShooting;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        // Componente está no prefab do player — GetComponent é direto e seguro aqui.
        playerMovement = GetComponent<PlayerMovement>();
        playerShooting = GetComponent<PlayerShooting>();

        netIsActive.OnValueChanged += OnActiveChanged;
    }

    public override void OnNetworkDespawn()
    {
        netIsActive.OnValueChanged -= OnActiveChanged;
        base.OnNetworkDespawn();
    }

    public void StartEffect(GameObject quemUsou, float jumpHeightModifier, float staticAimDuration,
        float bonusDamage, float bonusRadius, CommanderAbilityController controller, Ability ability)
    {
        if (!IsServer) return;

        netJumpHeightModifier.Value = jumpHeightModifier;
        netStaticAimDuration.Value = staticAimDuration;
        netBonusDamage.Value = bonusDamage;
        netBonusRadius.Value = bonusRadius;
        netIsActive.Value = true;
    }

    private void OnActiveChanged(bool oldVal, bool newVal)
    {
        if (!IsOwner) return;

        if (newVal)
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

            if (playerShooting != null)
                playerShooting.SetNextShotBonus(netBonusDamage.Value, netBonusRadius.Value);
        }
        else
        {
            if (playerMovement != null)
            {
                playerMovement.jumpHeightModifier = 1f;
                playerMovement.isFloating = false;
            }
        }
    }

    private void Update()
    {
        if (!netIsActive.Value || playerMovement == null) return;

        if (IsOwner && playerMovement.isGrounded)
            RequestDeactivateServerRpc();

        if (IsServer && playerMovement.isGrounded)
            Deactivate();
    }

    [ServerRpc]
    private void RequestDeactivateServerRpc()
    {
        Deactivate();
    }

    private void Deactivate()
    {
        if (!IsServer) return;
        netIsActive.Value = false;
    }
}
