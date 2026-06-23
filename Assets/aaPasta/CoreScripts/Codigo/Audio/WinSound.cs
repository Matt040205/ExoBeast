using UnityEngine;
using Unity.Netcode;
using FMODUnity;

/// <summary>
/// ── WinSound ────────────────────────────────────────────
/// Toca musica de vitoria de forma sincronizada via ClientRpc.
///
///  ▸ Server escolhe indice aleatorio em OnNetworkSpawn
///  ▸ PlayVictorySoundClientRpc: executa RuntimeManager.PlayOneShot em todos
///  ▸ Ativado pela cena de vitoria — sem logica adicional apos o spawn
/// ─────────────────────────────────────────────────────
/// </summary>
public class WinSound : NetworkBehaviour
{
    private string[] victoryEvents = new string[]
    {
        "event:/MUSiC/Victory_1",
        "event:/MUSiC/Victory_2"
    };

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();
        
        if (IsServer)
        {
            // Escolhe no servidor e avisa a todos
            int index = Random.Range(0, victoryEvents.Length);
            PlayVictorySoundClientRpc(index);
        }
    }

    [ClientRpc]
    private void PlayVictorySoundClientRpc(int index)
    {
        if (index >= 0 && index < victoryEvents.Length)
        {
            RuntimeManager.PlayOneShot(victoryEvents[index]);
        }
    }
}
