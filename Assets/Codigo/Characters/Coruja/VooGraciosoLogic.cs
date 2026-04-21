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

    [Header("VFX")]
    [SerializeField] private GameObject hoverVfxPrefab;

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
        // Lógica Zero-Latency: O dono já toca o efeito imediatamente
        if (IsOwner && hoverVfxPrefab != null)
        {
            GlobalVFXPool.GetVFX(hoverVfxPrefab, transform.position, transform.rotation, 2f);
        }

        // Se for servidor, aplica estado e faz o broadcast do VFX
        if (IsServer)
        {
            ApplyEffectServer(jumpHeightModifier, staticAimDuration, bonusDamage, bonusRadius);
            PlayHoverVfxClientRpc();
        }
        else
        {
            // Se for cliente, solicita ao servidor
            RequestStartEffectServerRpc(jumpHeightModifier, staticAimDuration, bonusDamage, bonusRadius);
        }
    }

    [ServerRpc]
    private void RequestStartEffectServerRpc(float jumpHeightModifier, float staticAimDuration, float bonusDamage, float bonusRadius)
    {
        ApplyEffectServer(jumpHeightModifier, staticAimDuration, bonusDamage, bonusRadius);
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

    [ClientRpc]
    private void PlayHoverVfxClientRpc()
    {
        if (IsOwner) return; // O dono já instanciou localmente com zero latency

        if (hoverVfxPrefab != null)
        {
            GlobalVFXPool.GetVFX(hoverVfxPrefab, transform.position, transform.rotation, 2f);
        }
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
