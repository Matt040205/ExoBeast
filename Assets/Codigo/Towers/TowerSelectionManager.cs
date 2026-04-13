using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;

public class TowerSelectionManager : MonoBehaviour
{
    public static TowerSelectionManager Instance;

    [Header("Painel de Upgrade (Torres)")]
    public UpgradePanelUI upgradePanel;

    [Header("Painel de Venda (Armadilhas)")]
    public GameObject trapSellPanel;
    public Button trapSellButton;
    public TextMeshProUGUI trapSellPriceText;

    [Header("Configuração da Seleção")]
    public LayerMask towerLayerMask;

    private TowerController selectedTower;
    private TrapLogicBase selectedTrap;
    private Component currentlyHighlighted;
    private Camera mainCamera;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    void Start()
    {
        mainCamera = Camera.main;
        if (mainCamera == null)
        {
            Debug.LogError("[TowerSelectionManager] Câmera principal não encontrada!");
            this.enabled = false;
        }

        if (trapSellButton != null)
        {
            trapSellButton.onClick.AddListener(SellSelectedTrap);
        }

        DeselectAll();
    }

    void Update()
    {
        if (Time.timeScale == 0 || mainCamera == null) return;

        if (!BuildManager.isBuildingMode || (BuildManager.Instance != null && BuildManager.Instance.IsHoldingBuilding))
        {
            if (currentlyHighlighted != null)
            {
                (currentlyHighlighted as TowerController)?.GetComponent<TowerSelectionCircle>()?.Unhighlight();
                currentlyHighlighted = null;
            }
            return;
        }

        HandleHoverHighlighting();
        HandleSelectionClick();
    }

    private void HandleHoverHighlighting()
    {
        if (EventSystem.current.IsPointerOverGameObject())
        {
            if (currentlyHighlighted != null)
            {
                (currentlyHighlighted as TowerController)?.GetComponent<TowerSelectionCircle>()?.Unhighlight();
                currentlyHighlighted = null;
            }
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        Component hitComponent = null;

        if (Physics.Raycast(ray, out hit, 1000f, towerLayerMask))
        {
            hitComponent = hit.collider.GetComponentInParent<TowerController>();
            if (hitComponent == null)
            {
                hitComponent = hit.collider.GetComponentInParent<TrapLogicBase>();
            }
        }

        if (hitComponent != currentlyHighlighted)
        {
            if (currentlyHighlighted != null)
            {
                (currentlyHighlighted as TowerController)?.GetComponent<TowerSelectionCircle>()?.Unhighlight();
            }

            if (hitComponent != null)
            {
                (hitComponent as TowerController)?.GetComponent<TowerSelectionCircle>()?.Highlight();
            }

            currentlyHighlighted = hitComponent;
        }
    }

    private void HandleSelectionClick()
    {
        if (Input.GetMouseButtonDown(0))
        {
            if (EventSystem.current.IsPointerOverGameObject())
            {
                return;
            }

            if (currentlyHighlighted != null)
            {
                TowerController tower = currentlyHighlighted as TowerController;
                if (tower != null)
                {
                    SelectTower(tower);
                }
                else
                {
                    TrapLogicBase trap = currentlyHighlighted as TrapLogicBase;
                    if (trap != null)
                    {
                        SelectTrap(trap);
                    }
                }
            }
            else
            {
                DeselectAll();
            }
        }
    }

    void SelectTower(TowerController tower)
    {
        if (tower == selectedTower && upgradePanel.IsPanelVisible())
        {
            DeselectAll();
        }
        else
        {
            DeselectAll();
            selectedTower = tower;
            if (upgradePanel != null)
            {
                upgradePanel.ShowPanel(selectedTower);
            }

            if (BuildManager.Instance != null)
            {
                BuildManager.Instance.ClearSelection();
            }
        }
    }

    void SelectTrap(TrapLogicBase trap)
    {
        if (trap == selectedTrap && trapSellPanel.activeSelf)
        {
            DeselectAll();
        }
        else
        {
            DeselectAll();
            selectedTrap = trap;

            if (trapSellPanel != null && trap.trapData != null)
            {
                float refundPercentage = trap.sellRefundPercentage;
                int geoditeRefund = Mathf.FloorToInt(trap.trapData.geoditeCost * refundPercentage);

                if (trapSellPriceText != null)
                {
                    trapSellPriceText.text = $"Vender por <color=#76D7C4>{geoditeRefund}G</color>";
                }

                trapSellPanel.SetActive(true);
            }
        }
    }

    void SellSelectedTrap()
    {
        if (selectedTrap != null)
        {
            selectedTrap.SellTrap();
        }
        DeselectAll();
    }

    public void DeselectAll()
    {
        selectedTower = null;
        selectedTrap = null;

        if (upgradePanel != null)
        {
            upgradePanel.HidePanel();
        }

        if (trapSellPanel != null)
        {
            trapSellPanel.SetActive(false);
        }
    }
}