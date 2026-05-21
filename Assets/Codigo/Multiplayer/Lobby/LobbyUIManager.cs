#if UNITY_EDITOR
using System;
using ExoBeasts.Managers;
using UnityEngine;

/// <summary>
/// Editor-only compatibility tombstone for stale Multiplayer Play Mode virtual projects.
/// The legacy IMGUI lobby flow was removed from scenes, but active MPPM clones can keep
/// generated compiler inputs that still reference this exact source path until the clone
/// is recreated. Keeping this tiny shim prevents CS2001 in those clones without restoring
/// the removed runtime lobby UI.
/// </summary>
[Obsolete("Legacy editor-only shim. Use LobbySceneUI for the supported lobby flow.")]
[AddComponentMenu("")]
public sealed class LobbyUIManager : MonoBehaviour
{
    public RectTransform painelSelecao;
    public RectTransform painelLobby;
    public Vector2 posSelecaoCentro = Vector2.zero;
    public Vector2 posSelecaoLado = new Vector2(-400f, 0f);
    public Vector2 posLobbyEscondido = new Vector2(1200f, 0f);
    public Vector2 posLobbyVisivel = new Vector2(450f, 0f);

    public void AbrirPainelMultiplayer()
    {
        Debug.LogWarning("[LobbyUIManager] Legacy editor-only shim invoked. Redirecting to LobbySceneUI flow.");
        GameModeManager.EnsureInstance().StartMultiplayer();
    }

    public void FecharPainelMultiplayer()
    {
        Debug.LogWarning("[LobbyUIManager] Legacy editor-only shim ignored FecharPainelMultiplayer().");
    }

    public void AlterarMaxPlayers(int quantidade)
    {
        Debug.LogWarning($"[LobbyUIManager] Legacy editor-only shim ignored AlterarMaxPlayers({quantidade}).");
    }
}
#endif
