using UnityEngine;
using Unity.Netcode;
using ExoBeasts.Multiplayer.Core;
/// <summary>
/// Força o desligamento seguro das conexões de rede e serviços (NGO e EOS) 
/// quando o jogador fecha o jogo ou sai do Play Mode no Editor.
/// Evita o congelamento infinito do Unity.
/// </summary>
public class MultiplayerCleanup : MonoBehaviour
{
    private void OnApplicationQuit()
    {
        Debug.Log("[Cleanup] Desligando processos de rede para evitar travamento do Editor...");

        if (NetworkManager.Singleton != null)
        {
            if (NetworkManager.Singleton.IsServer || NetworkManager.Singleton.IsClient)
            {
                NetworkManager.Singleton.Shutdown();
            }
        }

        if (EOSManagerWrapper.Instance != null)
        {
            EOSManagerWrapper.Instance.SendMessage("Shutdown", SendMessageOptions.DontRequireReceiver);
        }

        Debug.Log("[Cleanup] Limpeza concluída!");
    }
}