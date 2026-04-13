using UnityEngine;
using Unity.Netcode;

/// <summary>
/// ── TrapLogicBase ──────────────────────────────────────
/// Base abstrata para logica de armadilhas com venda sincronizada.
///
///  ▸ RequestSellServerRpc: servidor reembolsa custo e faz Despawn do NetworkObject
///  ▸ Classes derivadas implementam trigger de dano protegido por IsServer
/// ─────────────────────────────────────────────────────
/// </summary>
public abstract class TrapLogicBase : NetworkBehaviour
{
    public TrapDataSO trapData;
    public float sellRefundPercentage = 0.6f;
    protected bool iSBeingSoldOrDestroyed = false;

    public virtual void SellTrap()
    {
        if (iSBeingSoldOrDestroyed) return;
        
        // Solicitar ao servidor a venda do objeto
        RequestSellServerRpc();
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSellServerRpc()
    {
        if (iSBeingSoldOrDestroyed) return;
        iSBeingSoldOrDestroyed = true;

        if (trapData != null && CurrencyManager.Instance != null)
        {
            int geoditeRefund = Mathf.FloorToInt(trapData.geoditeCost * sellRefundPercentage);
            int etherRefund = Mathf.FloorToInt(trapData.darkEtherCost * sellRefundPercentage);

            if (geoditeRefund > 0)
            {
                CurrencyManager.Instance.AddCurrency(geoditeRefund, CurrencyType.Geodites);
            }
            if (etherRefund > 0)
            {
                CurrencyManager.Instance.AddCurrency(etherRefund, CurrencyType.DarkEther);
            }
        }

        // Remover da rede (destrói o objeto em todos os clientes sincronizadamente)
        if (TryGetComponent<NetworkObject>(out var netObj))
        {
            if (netObj.IsSpawned) netObj.Despawn();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    public override void OnNetworkDespawn()
    {
        // Limpeza local se necessário quando o objeto sumir da rede
        base.OnNetworkDespawn();
    }
}

