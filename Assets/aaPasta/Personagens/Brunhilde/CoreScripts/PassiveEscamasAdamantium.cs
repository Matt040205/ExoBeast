using UnityEngine;
using Unity.Netcode;

[CreateAssetMenu(fileName = "Escamas de Adamantium", menuName = "ExoBeasts/Personagens/Dragao/Passiva/Escamas de Adamantium")]
public class PassiveEscamasAdamantium : PassivaAbility
{
    [Range(0, 1)]
    public float towerHealthBonusPercent = 0.20f;
    [Range(0, 1)]
    public float playerDamageReduction = 0.20f;

    public override void OnEquip(GameObject owner)
    {
        if (!HasServerAuthority(owner))
            return;

        PlayerHealthSystem playerHealth = owner.GetComponent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.damageResistance.Value += playerDamageReduction;
        }

        TowerController[] towers = FindObjectsOfType<TowerController>();
        foreach (var tower in towers)
        {
            var health = tower.GetComponent<ObjectiveHealthSystem>();
            if (health != null)
            {
                float bonus = health.maxHealth * towerHealthBonusPercent;
                health.maxHealth += bonus;
                health.currentHealth.Value += bonus;
            }
        }
    }

    public override void OnUnequip(GameObject owner)
    {
        if (!HasServerAuthority(owner))
            return;

        PlayerHealthSystem playerHealth = owner.GetComponent<PlayerHealthSystem>();
        if (playerHealth != null)
        {
            playerHealth.damageResistance.Value -= playerDamageReduction;
        }

        TowerController[] towers = FindObjectsOfType<TowerController>();
        foreach (var tower in towers)
        {
            var health = tower.GetComponent<ObjectiveHealthSystem>();
            if (health != null)
            {
                float originalMaxHealth = health.maxHealth / (1 + towerHealthBonusPercent);
                float bonusToRemove = health.maxHealth - originalMaxHealth;

                health.maxHealth -= bonusToRemove;

                if (health.currentHealth.Value > health.maxHealth)
                {
                    health.currentHealth.Value = health.maxHealth;
                }
            }
        }
    }

    private bool HasServerAuthority(GameObject owner)
    {
        if (owner == null)
            return false;

        NetworkObject networkObject = owner.GetComponent<NetworkObject>();
        if (networkObject != null && networkObject.IsSpawned)
            return NetworkManager.Singleton != null && NetworkManager.Singleton.IsServer;

        return NetworkManager.Singleton == null ||
               !NetworkManager.Singleton.IsListening ||
               NetworkManager.Singleton.IsServer;
    }
}
