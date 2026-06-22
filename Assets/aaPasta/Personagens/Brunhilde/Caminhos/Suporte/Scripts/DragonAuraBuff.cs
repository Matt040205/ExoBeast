using UnityEngine;

public class DragonAuraBuff : MonoBehaviour
{
    public float DamageReduction { get; private set; }
    private float timer;
    private TowerController tower;
    private float appliedArmor;

    public void RefreshBuff(TowerController t, float armorBonus, float dmgReduction, float duration)
    {
        tower = t;
        timer = duration;

        if (appliedArmor == 0 && armorBonus > 0 && tower != null)
        {
            appliedArmor = armorBonus;
            tower.AddArmorBonus(appliedArmor);
        }
        DamageReduction = dmgReduction;
    }

    void Update()
    {
        if (timer > 0)
        {
            timer -= Time.deltaTime;
            if (timer <= 0)
            {
                if (appliedArmor > 0 && tower != null)
                {
                    tower.AddArmorBonus(-appliedArmor);
                    appliedArmor = 0;
                }
                DamageReduction = 0;
            }
        }
    }
    
    void OnDestroy()
    {
        if (appliedArmor > 0 && tower != null)
        {
            tower.AddArmorBonus(-appliedArmor);
        }
    }
}
