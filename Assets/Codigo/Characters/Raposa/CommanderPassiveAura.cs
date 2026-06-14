using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Unity.Netcode;

public class CommanderPassiveAura : MonoBehaviour
{
    public float attackSpeedBonus = 0.15f;
    public float auraRadius = 8f; // Cerca de 1.6 blocos no grid (onde 1 bloco = 5 unidades)

    private List<TowerController> affectedTowers = new List<TowerController>();
    private float timer;

    private bool IsServer => NetworkManager.Singleton == null || NetworkManager.Singleton.IsServer;

    private void Update()
    {
        if (!IsServer) return;

        // Se a Comandante estiver morta/respawnando, limpa a aura passiva
        var movement = GetComponent<PlayerMovement>();
        if (movement == null || !movement.enabled)
        {
            ClearAura();
            return;
        }

        timer += Time.deltaTime;
        if (timer >= 0.5f)
        {
            UpdateAura();
            timer = 0f;
        }
    }

    private void UpdateAura()
    {
        var towersInRange = FindTowersInRange();

        // Remove o buff de torres que saíram do alcance ou foram destruídas
        foreach (var tower in affectedTowers.ToList())
        {
            if (tower == null || !towersInRange.Contains(tower))
            {
                if (tower != null)
                {
                    tower.AddAttackSpeedBonus(-attackSpeedBonus);
                }
                affectedTowers.Remove(tower);
            }
        }

        // Aplica o buff em novas torres que entraram no alcance
        foreach (var tower in towersInRange)
        {
            if (!affectedTowers.Contains(tower))
            {
                tower.AddAttackSpeedBonus(attackSpeedBonus);
                affectedTowers.Add(tower);
            }
        }
    }

    private List<TowerController> FindTowersInRange()
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, auraRadius);
        return hits
            .Select(col => col.GetComponent<TowerController>())
            .Where(tower => tower != null)
            .Distinct()
            .ToList();
    }

    private void ClearAura()
    {
        foreach (var tower in affectedTowers)
        {
            if (tower != null)
            {
                tower.AddAttackSpeedBonus(-attackSpeedBonus);
            }
        }
        affectedTowers.Clear();
    }

    private void OnDestroy()
    {
        if (IsServer)
        {
            ClearAura();
        }
    }
}
