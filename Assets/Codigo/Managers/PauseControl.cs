using UnityEngine;
using Unity.Netcode;

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
        Debug.Log("[PauseControl] Congelando o jogo e chamando MenuManager...");

        if (MenuManager.Instance != null)
        {
            MenuManager.Instance.AbrirPause();
        }
        else
        {
            Debug.LogError("[PauseControl] ERRO: MenuManager.Instance está NULO! O script MenuManager não está na sua cena do Mapa!");
        }

        Time.timeScale = 0f;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        isPaused = true;
    }

    public void ResumeGame()
    {
        if (MenuManager.Instance != null) MenuManager.Instance.Resume();
    }
}