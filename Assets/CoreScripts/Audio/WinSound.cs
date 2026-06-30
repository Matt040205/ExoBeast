using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── WinSound ────────────────────────────────────────────
/// Toca musica de vitoria de forma sincronizada via ClientRpc.
///
///  ▸ Server escolhe indice aleatorio em OnNetworkSpawn
///  ▸ PlayVictorySoundClientRpc: executa ExoAudioService.PlayOneShot em todos
///  ▸ Ativado pela cena de vitoria — sem logica adicional apos o spawn
/// ─────────────────────────────────────────────────────
/// </summary>
public class WinSound : NetworkBehaviour
{
    private string[] victoryEvents = new string[]
    {
        AudioEventIds.MusicVictory1,
        AudioEventIds.MusicVictory2
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
            ExoAudioService.PlayOneShot(victoryEvents[index]);
        }
    }
}
