using UnityEngine;
using Unity.Netcode;

/// <summary>
/// Persistent network logic for Owl skill 1. The owner gets an explicit local
/// payload so the first cast does not depend on NetworkVariable delivery order.
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

    [Header("VFX")]
    [SerializeField] private GameObject hoverVfxPrefab;

    private PlayerMovement playerMovement;
    private PlayerShooting playerShooting;
    private float lastLocalHoverVfxTime = -999f;

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        CacheReferences();
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
        CacheReferences();

        if (IsOwner)
            ApplyOwnerEffectLocal(jumpHeightModifier, staticAimDuration, bonusDamage, bonusRadius, true);

        if (IsServer)
        {
            ApplyEffectServer(jumpHeightModifier, staticAimDuration, bonusDamage, bonusRadius);
            SendOwnerEffectToOwner(jumpHeightModifier, staticAimDuration, bonusDamage, bonusRadius);
            PlayHoverVfxClientRpc();
        }
        else
        {
            RequestStartEffectServerRpc(jumpHeightModifier, staticAimDuration, bonusDamage, bonusRadius);
        }
    }

    [ServerRpc]
    private void RequestStartEffectServerRpc(float jumpHeightModifier, float staticAimDuration, float bonusDamage, float bonusRadius)
    {
        ApplyEffectServer(jumpHeightModifier, staticAimDuration, bonusDamage, bonusRadius);
        SendOwnerEffectToOwner(jumpHeightModifier, staticAimDuration, bonusDamage, bonusRadius);
        PlayHoverVfxClientRpc();
    }

    private void ApplyEffectServer(float jumpHeightModifier, float staticAimDuration, float bonusDamage, float bonusRadius)
    {
        netJumpHeightModifier.Value = jumpHeightModifier;
        netStaticAimDuration.Value = staticAimDuration;
        netBonusDamage.Value = bonusDamage;
        netBonusRadius.Value = bonusRadius;
        netIsActive.Value = true;
    }

    private void SendOwnerEffectToOwner(float jumpHeightModifier, float staticAimDuration, float bonusDamage, float bonusRadius)
    {
        if (!IsServer || NetworkManager.Singleton == null || OwnerClientId == NetworkManager.ServerClientId)
            return;

        ClientRpcParams ownerOnly = new ClientRpcParams
        {
            Send = new ClientRpcSendParams
            {
                TargetClientIds = new[] { OwnerClientId }
            }
        };

        ApplyOwnerVooGraciosoClientRpc(jumpHeightModifier, staticAimDuration, bonusDamage, bonusRadius, ownerOnly);
    }

    [ClientRpc]
    private void ApplyOwnerVooGraciosoClientRpc(float jumpHeightModifier, float staticAimDuration, float bonusDamage, float bonusRadius, ClientRpcParams clientRpcParams = default)
    {
        if (!IsOwner)
            return;

        ApplyOwnerEffectLocal(jumpHeightModifier, staticAimDuration, bonusDamage, bonusRadius, true);
    }

    [ClientRpc]
    private void PlayHoverVfxClientRpc()
    {
        if (IsOwner)
            return;

        if (hoverVfxPrefab != null)
            GlobalVFXPool.GetVFX(hoverVfxPrefab, transform.position, transform.rotation, 2f);
    }

    private void CacheReferences()
    {
        if (playerMovement == null)
            playerMovement = GetComponent<PlayerMovement>();

        if (playerShooting == null)
            playerShooting = GetComponent<PlayerShooting>();
    }

    private void ApplyOwnerEffectLocal(float jumpHeightModifier, float staticAimDuration, float bonusDamage, float bonusRadius, bool playVfx)
    {
        CacheReferences();

        if (playVfx)
            PlayHoverVfxLocal();

        if (playerMovement != null)
        {
            if (jumpHeightModifier > 0f)
                playerMovement.jumpHeightModifier = jumpHeightModifier;

            if (!playerMovement.isGrounded && staticAimDuration > 0f)
            {
                playerMovement.isFloating = true;
                playerMovement.floatDuration = Mathf.Max(playerMovement.floatDuration, staticAimDuration);
            }
        }

        if (playerShooting != null && bonusDamage > 0f)
            playerShooting.SetNextShotBonus(bonusDamage, Mathf.Max(0f, bonusRadius));
    }

    private void PlayHoverVfxLocal()
    {
        if (hoverVfxPrefab == null || Time.time - lastLocalHoverVfxTime < 0.1f)
            return;

        lastLocalHoverVfxTime = Time.time;
        GlobalVFXPool.GetVFX(hoverVfxPrefab, transform.position, transform.rotation, 2f);
    }

    private void OnActiveChanged(bool oldVal, bool newVal)
    {
        if (!IsOwner)
            return;

        if (newVal)
        {
            ApplyOwnerEffectLocal(
                netJumpHeightModifier.Value,
                netStaticAimDuration.Value,
                netBonusDamage.Value,
                netBonusRadius.Value,
                false);
        }
        else
        {
            CacheReferences();

            if (playerMovement != null)
            {
                playerMovement.jumpHeightModifier = 1f;
                playerMovement.isFloating = false;
                playerMovement.floatDuration = 0f;
            }
        }
    }

    private void Update()
    {
        CacheReferences();

        if (!netIsActive.Value || playerMovement == null)
            return;

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
        if (!IsServer)
            return;

        netIsActive.Value = false;
    }
}
