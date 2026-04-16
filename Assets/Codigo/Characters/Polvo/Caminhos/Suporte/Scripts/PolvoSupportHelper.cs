using UnityEngine;
using System.Collections;
using Unity.Netcode;

public class PolvoSupportHelper : MonoBehaviour
{
    public static void ApplyHealAndBuff(GameObject target, TowerController healerTower)
    {
        float multiplier = healerTower.GetComponent<SupportHealBoostBehavior>() != null ? 1.5f : 1f;
        bool shouldBuff = healerTower.GetComponent<SupportBuffBehavior>() != null;

        PlayerHealthSystem player = target.GetComponent<PlayerHealthSystem>();
        if (player != null)
        {
            float maxH = player.characterData != null ? player.characterData.maxHealth : 100f;
            player.Heal(maxH * 0.01f * multiplier);
            if (shouldBuff) player.ApplyBuffs(1.15f, 1.15f, 3f);
        }
        else
        {
            TowerController otherTower = target.GetComponent<TowerController>();
            if (otherTower != null && otherTower != healerTower)
            {
                otherTower.Heal(otherTower.MaxHealth * 0.01f * multiplier);
                if (shouldBuff)
                {
                    TemporaryTowerBuff tempBuff = otherTower.gameObject.GetComponent<TemporaryTowerBuff>();
                    if (tempBuff == null) tempBuff = otherTower.gameObject.AddComponent<TemporaryTowerBuff>();
                    tempBuff.ApplyBuff(otherTower, 0.15f, 3f);
                }
            }
        }
    }
}

public class TemporaryTowerBuff : MonoBehaviour
{
    private TowerController tower;
    private float timer;
    private bool isActive;
    private float amount;

    public void ApplyBuff(TowerController t, float amt, float duration)
    {
        tower = t;
        timer = duration;
        if (!isActive)
        {
            amount = amt;
            isActive = true;
            tower.AddDamageBonus(amount);
            tower.AddAttackSpeedBonus(amount);
        }
    }

    void Update()
    {
        if (!isActive) return;
        timer -= Time.deltaTime;
        if (timer <= 0)
        {
            tower.AddDamageBonus(-amount);
            tower.AddAttackSpeedBonus(-amount);
            isActive = false;
        }
    }
    
    void OnDestroy()
    {
        if (isActive && tower != null)
        {
            tower.AddDamageBonus(-amount);
            tower.AddAttackSpeedBonus(-amount);
        }
    }
}
