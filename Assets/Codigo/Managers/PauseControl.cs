using UnityEngine;

/// <summary>
/// ── PauseControl ────────────────────────────────────
/// Controle de pause local (visual-only, sem Time.timeScale).
///
///  ▸ MonoBehaviour simples — funciona tanto em cena standalone quanto em sessao de rede
///  ▸ O pause eh local por cliente; nao propaga pela rede (intencional)
///  ▸ Delega UI para MenuManager.AbrirPause() / Resume()
///  ▸ CORREÇÃO: Removida heranca de NetworkBehaviour (era necessaria estar no prefab
///    do jogador para ter IsOwner == true; como fica na cena, nunca capturava input)
/// ─────────────────────────────────────────────────────
/// </summary>
public class PauseControl : MonoBehaviour
{
    public static bool isPaused = false;

    public KeyCode teclaPausePrincipal = KeyCode.Escape;
    public KeyCode teclaPauseSecundaria = KeyCode.P;

    void Update()
    {
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
