using UnityEngine;
using System.Collections;
using FMODUnity;
using FMOD.Studio;
using Unity.Netcode;

/// <summary>
/// ── PeaceOfMindLogic ─────────────────────────────────────
/// Heals the player gradually with FMOD audio synced to all clients.
///
///  ▸ Owner requests heal: StartEffect → RequestPeaceOfMindServerRpc
///  ▸ Server applies health changes via PlayerHealthSystem.Heal
///  ▸ PlayHealSFXClientRpc / StopHealSFXClientRpc keep audio in sync across all clients
/// ─────────────────────────────────────────────────────────
/// </summary>
public class PeaceOfMindLogic : NetworkBehaviour
{
    private PlayerHealthSystem healthSystem;
    private EventInstance curaSoundInstance;

    [Header("FMOD")]
    [EventRef]
    public string eventoCura = "event:/SFX/Cura";

    public void StartEffect(float totalHeal, float duration, Ability sourceAbility)
    {
        if (!IsOwner) return;

        RequestPeaceOfMindServerRpc(totalHeal, duration);
    }

    [ServerRpc]
    private void RequestPeaceOfMindServerRpc(float totalHeal, float duration)
    {
        healthSystem = GetComponent<PlayerHealthSystem>();
        if (healthSystem != null)
        {
            PlayHealSFXClientRpc();
            StartCoroutine(HealCoroutine(totalHeal, duration));
        }
    }

    [ClientRpc]
    private void PlayHealSFXClientRpc()
    {
        if (!string.IsNullOrEmpty(eventoCura))
        {
            curaSoundInstance = RuntimeManager.CreateInstance(eventoCura);
            RuntimeManager.AttachInstanceToGameObject(curaSoundInstance, transform);
            curaSoundInstance.start();
        }
    }

    private IEnumerator HealCoroutine(float totalHeal, float duration)
    {
        float healPerSecond = totalHeal / duration;
        float timeLeft = duration;

        while (timeLeft > 0)
        {
            healthSystem.Heal(healPerSecond * Time.deltaTime);
            timeLeft -= Time.deltaTime;
            yield return null;
        }

        StopHealSFXClientRpc();
        // Disable instead of destroy — this component lives on the player's NetworkObject
        this.enabled = false;
    }

    [ClientRpc]
    private void StopHealSFXClientRpc()
    {
        if (curaSoundInstance.isValid())
        {
            curaSoundInstance.stop(FMOD.Studio.STOP_MODE.ALLOWFADEOUT);
            curaSoundInstance.release();
        }
    }

    public override void OnNetworkDespawn()
    {
        if (curaSoundInstance.isValid())
        {
            curaSoundInstance.stop(FMOD.Studio.STOP_MODE.IMMEDIATE);
            curaSoundInstance.release();
        }
        base.OnNetworkDespawn();
    }
}
