using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class DragonShieldGeneratorBehavior : TowerBehavior
{
    [Header("Configurações de Escudo")]
    public float auraRange = 10f;
    public LayerMask allyLayer;

    [Header("Visual")]
    public GameObject shieldVfxPrefab;

    public override void Initialize(TowerController owner)
    {
        base.Initialize(owner);
        if (!IsServer) return;
        if (allyLayer.value == 0) allyLayer = LayerMask.GetMask("Player", "Tower");
        StartCoroutine(ShieldRoutine());
    }

    private IEnumerator ShieldRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(10f); // Gera escudo a cada 10s
            if (towerController == null) yield break;

            float shieldAmount = towerController.GetComponent<DragonShieldBoostBehavior>() != null ? 100f : 50f;
            bool canExplode = towerController.GetComponent<DragonShieldExplosionBehavior>() != null;

            Collider[] allies = Physics.OverlapSphere(transform.position, auraRange, allyLayer);
            foreach (var ally in allies)
            {
                if (ally.gameObject != gameObject) // Não aplica escudo a ela mesma
                {
                    AllyShield shield = ally.GetComponent<AllyShield>();
                    if (shield == null) shield = ally.gameObject.AddComponent<AllyShield>();
                    
                    if (ally.TryGetComponent<NetworkObject>(out NetworkObject netObj))
                    {
                        shield.ApplyShield(shieldAmount, towerController, canExplode, netObj.NetworkObjectId, SendShieldBrokenRPC);
                        SetShieldVisualState(netObj.NetworkObjectId, true);
                    }
                    else
                    {
                        shield.ApplyShield(shieldAmount, towerController, canExplode);
                    }
                }
            }
        }
    }

    private void SendShieldBrokenRPC(ulong targetNetId)
    {
        if (towerController != null && towerController.GetComponent<NetworkObject>() != null && towerController.GetComponent<NetworkObject>().IsSpawned)
        {
            SetShieldVisualState(targetNetId, false);
        }
        else
        {
            // Fallback seguro caso essa torre tenha sido destruída, mas o escudo do alvo acabou de quebrar
            DragonShieldGeneratorBehavior anyTower = FindFirstObjectByType<DragonShieldGeneratorBehavior>();
            if (anyTower != null && anyTower.towerController != null && anyTower.towerController.GetComponent<NetworkObject>() != null && anyTower.towerController.GetComponent<NetworkObject>().IsSpawned)
            {
                anyTower.SetShieldVisualState(targetNetId, false);
            }
        }
    }

    private void SetShieldVisualState(ulong targetNetId, bool isActive)
    {
        if (towerController != null)
        {
            var networkedBuilding = towerController.GetComponent<ExoBeasts.Multiplayer.Sync.NetworkedBuilding>();
            if (networkedBuilding != null && networkedBuilding.IsSpawned)
            {
                networkedBuilding.BroadcastShieldVisualStateClientRpc(targetNetId, isActive);
            }
        }
    }

    public void ApplyShieldVisualStateLocal(ulong targetNetId, bool isActive)
    {
        if (shieldVfxPrefab == null || NetworkManager.Singleton == null) return;
        
        if (NetworkManager.Singleton.SpawnManager.SpawnedObjects.TryGetValue(targetNetId, out NetworkObject targetNetObj))
        {
            AllyShieldVisual visual = targetNetObj.GetComponent<AllyShieldVisual>();
            if (visual == null)
            {
                visual = targetNetObj.gameObject.AddComponent<AllyShieldVisual>();
                visual.shieldPrefab = shieldVfxPrefab;
            }
            visual.SetActive(isActive);
        }
    }
}
