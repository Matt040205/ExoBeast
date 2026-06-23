using UnityEngine;
using Unity.Netcode;
using FMODUnity;

/// <summary>
/// ── LoseSound ───────────────────────────────────────────
/// Toca musica de derrota de forma sincronizada via ClientRpc.
///
///  ▸ Server escolhe indice aleatorio em OnNetworkSpawn
///  ▸ PlayLoseSoundClientRpc: executa RuntimeManager.PlayOneShot em todos
///  ▸ Ativado pela cena de derrota — sem logica adicional apos o spawn
/// ─────────────────────────────────────────────────────
/// </summary>
public class LoseSound : NetworkBehaviour
{
    private string[] loseEvents = new string[]
    {
        "event:/MUSiC/Lose_1",
        "event:/MUSiC/Lose_2"
    };

    public override void OnNetworkSpawn()
    {
        base.OnNetworkSpawn();

        if (IsServer)
        {
            int index = Random.Range(0, loseEvents.Length);
            PlayLoseSoundClientRpc(index);
        }
    }

    [ClientRpc]
    private void PlayLoseSoundClientRpc(int index)
    {
        if (index >= 0 && index < loseEvents.Length)
        {
            RuntimeManager.PlayOneShot(loseEvents[index]);
        }
    }
}
