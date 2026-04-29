using UnityEngine;
using UnityEngine.InputSystem;

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
    private static PauseControl activeInstance;

    public KeyCode teclaPausePrincipal = KeyCode.Escape;
    public KeyCode teclaPauseSecundaria = KeyCode.P;

    void Awake()
    {
        // Apenas a primeira instância processa input
        if (activeInstance != null && activeInstance != this)
        {
            this.enabled = false;
            return;
        }
        activeInstance = this;
    }

    void OnDestroy()
    {
        if (activeInstance == this) activeInstance = null;
    }

    void Update()
    {
        if (Input.GetKeyDown(teclaPausePrincipal) || Input.GetKeyDown(teclaPauseSecundaria))
        {
            if (isPaused) ResumeGame();
            else PauseGame();
        }
    }

    public void OnPause(InputAction.CallbackContext ctx)
    {
        if (!ctx.performed)
            return;

        if (isPaused) ResumeGame();
        else PauseGame();
    }

    public void PauseGame()
    {
        isPaused = true;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowPauseMenu(true);
        }
        else if (MenuManager.Instance != null)
        {
            MenuManager.Instance.AbrirPause();
        }

        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void ResumeGame()
    {
        isPaused = false;

        if (UIManager.Instance != null)
        {
            UIManager.Instance.ShowPauseMenu(false);
        }
        else if (MenuManager.Instance != null)
        {
            MenuManager.Instance.Resume();
        }

        Cursor.lockState = BuildManager.isBuildingMode ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = BuildManager.isBuildingMode;
    }
}
