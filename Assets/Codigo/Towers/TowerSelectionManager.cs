using UnityEngine;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using TMPro;
using UnityEngine.UI;
using ExoBeasts.Multiplayer.Sync;

public class TowerSelectionManager : MonoBehaviour
{
    public static TowerSelectionManager Instance;

    [Header("Painel de Upgrade (Torres)")]
    public UpgradePanelUI upgradePanel;

    [Header("Painel de Venda (Armadilhas)")]
    public GameObject trapSellPanel;
    public Button trapSellButton;
    public TextMeshProUGUI trapSellPriceText;

    [Header("Configura��o da Sele��o")]
    public LayerMask towerLayerMask;

    private TowerController selectedTower;
    private NetworkedTrapVisual selectedTrap;
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
            Debug.LogError("[TowerSelectionManager] C�mera principal n�o encontrada!");
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
        if (EventSystem.current.IsPointerOverGameObject()) { Debug.Log("[TowerSelection] Mouse is over UI!"); if (currentlyHighlighted != null)
            {
                (currentlyHighlighted as TowerController)?.GetComponent<TowerSelectionCircle>()?.Unhighlight();
                currentlyHighlighted = null;
            }
            return;
        }

        Ray ray = mainCamera.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit; Component hitComponent = null; Debug.Log($"[TowerSelection] Raycast shot at {Input.mousePosition}");

        if (Physics.Raycast(ray, out hit, 1000f, towerLayerMask))
        {
            hitComponent = hit.collider.GetComponentInParent<TowerController>();
            if (hitComponent is TowerController tower)
            {
                NetworkedBuilding networkedBuilding = tower.GetComponent<NetworkedBuilding>();
                if (networkedBuilding != null && !networkedBuilding.CanInteractLocally())
                    hitComponent = null;
            }

            if (hitComponent == null)
            {
                NetworkedTrapVisual trapVisual = hit.collider.GetComponentInParent<NetworkedTrapVisual>();
                if (trapVisual != null && trapVisual.CanInteractLocally())
                    hitComponent = trapVisual;
            }
        }

        Debug.Log($"[TowerSelection] Hit component: {(hitComponent != null ? hitComponent.name : "null")}"); if (hitComponent != currentlyHighlighted)
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
                    NetworkedTrapVisual trap = currentlyHighlighted as NetworkedTrapVisual;
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
        NetworkedBuilding networkedBuilding = tower != null ? tower.GetComponent<NetworkedBuilding>() : null;
        if (networkedBuilding != null && !networkedBuilding.CanInteractLocally())
            return;

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

    void SelectTrap(NetworkedTrapVisual trap)
    {
        if (trap != null && !trap.CanInteractLocally())
            return;

        if (trap == selectedTrap && trapSellPanel.activeSelf)
        {
            DeselectAll();
        }
        else
        {
            DeselectAll();
            selectedTrap = trap;

            if (trapSellPanel != null && trap.TrapData != null)
            {
                float refundPercentage = trap.SellRefundPercentage;
                int geoditeRefund = Mathf.FloorToInt(trap.TrapData.geoditeCost * refundPercentage);

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
