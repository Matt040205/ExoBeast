using Unity.Netcode;
using UnityEngine;

public abstract class TrapLogicBase : NetworkBehaviour
{
    public TrapDataSO trapData;
    public float sellRefundPercentage = 0.6f;

    protected bool iSBeingSoldOrDestroyed;

    public ulong BuilderClientId { get; private set; }
    public ulong VisualObjectId { get; private set; }

    public virtual void InitializeServer(TrapDataSO newTrapData, ulong builderClientId, ulong visualObjectId)
    {
        trapData = newTrapData;
        BuilderClientId = builderClientId;
        VisualObjectId = visualObjectId;
    }

    public virtual void BindVisualServer(ulong visualObjectId)
    {
        VisualObjectId = visualObjectId;
    }

    public virtual void SellTrap()
    {
        if (iSBeingSoldOrDestroyed)
            return;

        RequestSellServerRpc();
    }

    protected bool TryResolveVisual(out ExoBeasts.Multiplayer.Sync.NetworkedTrapVisual trapVisual)
    {
        trapVisual = null;

        if (VisualObjectId == 0 ||
            NetworkManager.Singleton == null ||
            !NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(VisualObjectId, out NetworkObject visualObject))
        {
            return false;
        }

        trapVisual = visualObject.GetComponent<ExoBeasts.Multiplayer.Sync.NetworkedTrapVisual>();
        return trapVisual != null;
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSellServerRpc(ServerRpcParams rpcParams = default)
    {
        if (iSBeingSoldOrDestroyed || !CanRequesterModify(rpcParams.Receive.SenderClientId))
            return;

        iSBeingSoldOrDestroyed = true;

        if (trapData != null && CurrencyManager.Instance != null)
        {
            int geoditeRefund = Mathf.FloorToInt(trapData.geoditeCost * sellRefundPercentage);
            int etherRefund = Mathf.FloorToInt(trapData.darkEtherCost * sellRefundPercentage);

            if (geoditeRefund > 0)
                CurrencyManager.Instance.AddCurrency(geoditeRefund, CurrencyType.Geodites);

            if (etherRefund > 0)
                CurrencyManager.Instance.AddCurrency(etherRefund, CurrencyType.DarkEther);
        }

        if (TryResolveVisual(out ExoBeasts.Multiplayer.Sync.NetworkedTrapVisual trapVisual) &&
            trapVisual.NetworkObject != null &&
            trapVisual.NetworkObject.IsSpawned)
        {
            trapVisual.NetworkObject.Despawn(true);
        }

        if (TryGetComponent(out NetworkObject netObj) && netObj.IsSpawned)
            netObj.Despawn(true);
        else
            Destroy(gameObject);
    }

    private bool CanRequesterModify(ulong senderClientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.IsListening)
            return true;

        return senderClientId == BuilderClientId;
    }
}
