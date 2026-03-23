using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Netcode;
using System.Collections;
using System.Collections.Generic;

public class MenuManager : MonoBehaviour
{
    public static MenuManager Instance;

    public GameObject menuPanel;
    public GameObject optionsPanel;
    public GameObject pausePanel;
    public GameObject hudPanel;
    public List<GameObject> pauseButtons = new List<GameObject>();
    public float optionsCenterX = -117f;

    private void Awake() => Instance = this;

    void Start()
    {
        if (optionsPanel) optionsPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);

        if (hudPanel != null) { if (menuPanel) menuPanel.SetActive(false); }
        else { if (menuPanel) menuPanel.SetActive(true); }
    }

    public void AbrirPause()
    {
        Time.timeScale = 0f;
        PauseControl.isPaused = true;
        if (hudPanel) hudPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(true);
        SetPauseButtonsState(true);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
    }

    public void Resume()
    {
        Time.timeScale = 1f;
        PauseControl.isPaused = false;
        if (optionsPanel) optionsPanel.SetActive(false);
        if (pausePanel) pausePanel.SetActive(false);
        if (hudPanel) hudPanel.SetActive(!BuildManager.isBuildingMode);
        Cursor.lockState = BuildManager.isBuildingMode ? CursorLockMode.None : CursorLockMode.Locked;
        Cursor.visible = BuildManager.isBuildingMode;
    }

    public void Options()
    {
        StopAllCoroutines();
        StartCoroutine(ForcarAberturaOptions());
    }

    private IEnumerator ForcarAberturaOptions()
    {
        SetPauseButtonsState(false);

        yield return null;

        if (optionsPanel != null)
        {
            optionsPanel.SetActive(true);

            RectTransform rt = optionsPanel.GetComponent<RectTransform>();
            if (rt != null)
            {
                rt.anchoredPosition = new Vector2(optionsCenterX, 0f);
            }

            optionsPanel.transform.SetAsLastSibling();
        }

        Canvas.ForceUpdateCanvases();
    }

    public void BotaoBack()
    {
        if (optionsPanel) optionsPanel.SetActive(false);
        SetPauseButtonsState(true);
    }

    private void SetPauseButtonsState(bool state)
    {
        foreach (GameObject btn in pauseButtons)
        {
            if (btn != null) btn.SetActive(state);
        }
    }

    public void ChangeScene(string nomeDaCena)
    {
        Time.timeScale = 1f;
        PauseControl.isPaused = false;
        if (NetworkManager.Singleton != null) NetworkManager.Singleton.Shutdown();
        SceneManager.LoadScene(nomeDaCena);
    }
}