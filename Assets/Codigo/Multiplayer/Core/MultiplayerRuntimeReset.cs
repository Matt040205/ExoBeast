using System.Collections;
using ExoBeasts.Managers;
using ExoBeasts.Multiplayer.Auth;
using ExoBeasts.Multiplayer.Lobby;
using Unity.Netcode;
using UnityEngine;

namespace ExoBeasts.Multiplayer.Core
{
    /// <summary>
    /// Centraliza a limpeza de lobby, caches autoritativos e NGO
    /// antes de mudar entre singleplayer, multiplayer e play direto.
    /// </summary>
    public static class MultiplayerRuntimeReset
    {
        public static IEnumerator ResetToOfflineLocal(float shutdownTimeoutSeconds = 3f)
        {
            ApplyOfflineLocalState();

            LobbyManager lobby = LobbyManager.TryGetExistingInstance();
            if (lobby != null)
                lobby.ForceResetRuntimeState();

            NetworkManager networkManager = NetworkManager.Singleton;
            if (networkManager == null)
                yield break;

            networkManager.ConnectionApprovalCallback = null;
            networkManager.NetworkConfig.ConnectionApproval = false;

            if (!IsNetworkRuntimeActive(networkManager))
                yield break;

            networkManager.Shutdown();

            float elapsed = 0f;
            while (networkManager != null && networkManager.IsListening && elapsed < shutdownTimeoutSeconds)
            {
                elapsed += Time.unscaledDeltaTime > 0f ? Time.unscaledDeltaTime : Time.deltaTime;
                yield return null;
            }

            if (networkManager != null)
            {
                networkManager.ConnectionApprovalCallback = null;
                networkManager.NetworkConfig.ConnectionApproval = false;

                if (networkManager.IsListening)
                {
                    Debug.LogWarning($"[MultiplayerRuntimeReset] O NetworkManager nao encerrou totalmente apos {shutdownTimeoutSeconds:0.##}s.");
                }
            }
        }

        public static void ApplyOfflineLocalState()
        {
            GameModeManager.ReturnToSingleplayer();

            SessionManager session = SessionManager.TryGetExistingInstance();
            if (session != null)
            {
                session.SetCurrentLobby(string.Empty);
                session.SetCurrentMatch(string.Empty);
            }

            CharacterChoiceCache.Clear();

            LobbyManager lobby = LobbyManager.TryGetExistingInstance();
            if (lobby != null)
                lobby.CancelPendingClientConnect();
        }

        private static bool IsNetworkRuntimeActive(NetworkManager networkManager)
        {
            return networkManager != null &&
                   (networkManager.IsListening ||
                    networkManager.IsHost ||
                    networkManager.IsServer ||
                    networkManager.IsClient);
        }
    }
}
