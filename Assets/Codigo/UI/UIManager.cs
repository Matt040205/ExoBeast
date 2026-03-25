using UnityEngine;
using UnityEngine.UI;
using System.Collections.Generic;
using TMPro;

/// <summary>
/// ── UIManager ───────────────────────────────────────
/// Gerencia paineis de UI da cena de jogo (HUD, pause, build, loja).
///
///  ▸ Timer: le MatchManager.MatchTime (NetworkVariable) com fallback local
///  ▸ Vida do objetivo: observa OnHealthChanged do ObjectiveHealthSystem
///  ▸ Paineis: HUD, pause e build com transicoes mutuamente exclusivas
///  ▸ Nao usa Time.timeScale — pause eh visual-only
/// ─────────────────────────────────────────────────────
/// </summary>
public class UIManager : MonoBehaviour
{
    public static UIManager Instance;
    public GameObject hudPanel;
    public GameObject pausePanel;
    public GameObject buildPanel;
    public BuildButtonUI buildButtonUI;

    public TextMeshProUGUI timerText;

    public ObjectiveHealthSystem objectiveHealthSystem;
    public TextMeshProUGUI objectiveHealthText;
    public Image objectiveHealthBar;

    public Button towerShopButton;
    public Button trapShopButton;
    public GameObject towerShopPanel;
    public GameObject trapShopPanel;

    private float gameTime = 0f;

    private void Awake()
    {
        Instance = this;
    }

    void Start()
    {
        ShowHUD();

        if (objectiveHealthSystem != null)
        {
            objectiveHealthSystem.OnHealthChanged += UpdateObjectiveHealthUI;
            UpdateObjectiveHealthUI();
        }

        if (towerShopButton != null)
            towerShopButton.onClick.AddListener(ShowTowerShop);

        if (trapShopButton != null)
            trapShopButton.onClick.AddListener(ShowTrapShop);

        if (buildPanel != null && buildPanel.activeInHierarchy)
        {
            ShowTowerShop();
        }
    }

    void Update()
    {
        if (ExoBeasts.Multiplayer.GameServer.MatchManager.Instance != null)
        {
            gameTime = ExoBeasts.Multiplayer.GameServer.MatchManager.Instance.MatchTime.Value;
        }
        else
        {
            gameTime += Time.deltaTime;
        }
        
        UpdateTimerDisplay(gameTime);
    }

    private void OnDestroy()
    {
        if (objectiveHealthSystem != null)
        {
            objectiveHealthSystem.OnHealthChanged -= UpdateObjectiveHealthUI;
        }
    }

    public void UpdateObjectiveHealthUI()
    {
        if (objectiveHealthSystem == null) return;

        float currentHealth = objectiveHealthSystem.currentHealth.Value;
        float maxHealth = objectiveHealthSystem.maxHealth;

        if (objectiveHealthText != null)
        {
            objectiveHealthText.text = $"{currentHealth:F0} / {maxHealth:F0}";
        }

        if (objectiveHealthBar != null)
        {
            if (maxHealth > 0)
            {
                objectiveHealthBar.fillAmount = currentHealth / maxHealth;
            }
            else
            {
                objectiveHealthBar.fillAmount = 0;
            }
        }
    }

    public void UpdateBuildUI(List<CharacterBase> towers, List<TrapDataSO> traps)
    {
        if (buildButtonUI != null)
        {
            buildButtonUI.ClearTowerButtons();
            buildButtonUI.CreateTowerBuildButtons(towers);

            buildButtonUI.ClearTrapButtons();
            buildButtonUI.CreateTrapBuildButtons(traps);
        }
    }

    public void ShowHUD()
    {
        if (hudPanel != null) hudPanel.SetActive(true);
        if (pausePanel != null) pausePanel.SetActive(false);
        if (buildPanel != null) buildPanel.SetActive(false);
    }

    public void ShowPauseMenu(bool show)
    {
        if (pausePanel != null)
            pausePanel.SetActive(show);

        if (show)
        {
            if (hudPanel != null) hudPanel.SetActive(false);
        }
        else
        {
            if (BuildManager.isBuildingMode)
            {
                ShowBuildUI(true);
            }
            else
            {
                ShowHUD();
            }
        }
    }

    public void ShowBuildUI(bool show)
    {
        if (buildPanel != null) buildPanel.SetActive(show);
        if (show)
        {
            if (hudPanel != null) hudPanel.SetActive(false);
            ShowTowerShop();
        }
        else
        {
            ShowHUD();
        }
    }

    public void ShowTowerShop()
    {
        if (towerShopPanel != null) towerShopPanel.SetActive(true);
        if (trapShopPanel != null) trapShopPanel.SetActive(false);

        if (towerShopButton != null) towerShopButton.interactable = false;
        if (trapShopButton != null) trapShopButton.interactable = true;
    }

    public void ShowTrapShop()
    {
        if (towerShopPanel != null) towerShopPanel.SetActive(false);
        if (trapShopPanel != null) trapShopPanel.SetActive(true);

        if (towerShopButton != null) towerShopButton.interactable = true;
        if (trapShopButton != null) trapShopButton.interactable = false;
    }

    public void UpdateTimerDisplay(float timeInSeconds)
    {
        if (timerText == null) return;

        int minutes = Mathf.FloorToInt(timeInSeconds / 60);
        int seconds = Mathf.FloorToInt(timeInSeconds % 60);

        timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
    }
}
