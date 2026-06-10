using UnityEngine;
using System.Collections;
using FMODUnity;
using FMOD.Studio;
using Unity.Netcode;
using Unity.Netcode.Components;

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
    public string eventoCura = "event:/Player/Heal";

    public void StartEffect(float totalHeal, float duration, Ability sourceAbility)
    {
        // BUG FIX (Bug 3 - 7 Maio 2026): antes esta chamada usava RequestPeaceOfMindServerRpc
        // sem RequireOwnership=false. Como Activate() ja roda no servidor (via
        // RequestActivateAbilityServerRpc no CommanderAbilityController) e o servidor (clientId 0)
        // NAO eh owner do player Raposa (cliente clientId 4), o NGO REJEITAVA a ServerRpc com
        // warning silencioso por ownership mismatch. Resultado: cura nunca aplicava.
        // Correcao: replicar padrao do NineTailsDanceLogic.StartEffect — rodar direto no servidor.
        if (!IsServer) return;

        healthSystem = GetComponent<PlayerHealthSystem>();
        if (healthSystem != null)
        {
            PlayHealAnimationClientRpc();
            PlayHealSFXClientRpc();
            StartCoroutine(HealCoroutine(totalHeal, duration));
        }
    }

    // NetworkAnimator.SetTrigger só propaga quando chamado pelo owner.
    // Enviar de volta ao owner para que a propagação automática do NGO alcance todos.
    [ClientRpc]
    private void PlayHealAnimationClientRpc()
    {
        if (!IsOwner) return;
        var netAnim = GetComponent<NetworkAnimator>() ?? GetComponentInChildren<NetworkAnimator>();
        if (netAnim != null) netAnim.SetTrigger("Heal");
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
