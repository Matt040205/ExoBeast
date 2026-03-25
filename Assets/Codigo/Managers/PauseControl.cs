using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── PauseControl ────────────────────────────────────
/// Controle de pause local por jogador (visual-only, sem Time.timeScale).
///
///  ▸ Owner: detecta input Escape/P para toggle de pause
///  ▸ isPaused eh static local — cada cliente pausa independentemente
///  ▸ Delega UI para MenuManager.AbrirPause() / Resume()
/// ─────────────────────────────────────────────────────
/// </summary>
public class PauseControl : NetworkBehaviour
{
    public static bool isPaused = false;

    public KeyCode teclaPausePrincipal = KeyCode.Escape;
    public KeyCode teclaPauseSecundaria = KeyCode.P;

    void Update()
    {
        if (!IsOwner) return;

        if (Input.GetKeyDown(teclaPausePrincipal) || Input.GetKeyDown(teclaPauseSecundaria))
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    public void PauseGame()
    {
        if (MenuManager.Instance != null)
            MenuManager.Instance.AbrirPause();

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isPaused = true;
    }

    public void ResumeGame()
    {
        if (MenuManager.Instance != null) MenuManager.Instance.Resume();
    }
}
