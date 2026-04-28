using UnityEngine;

public struct DamageRequest
{
    public float BaseDamage;
    public Transform Attacker;
    public bool IsMelee;
    public ulong AttackerClientId;

    public DamageRequest(float baseDamage, Transform attacker, bool isMelee, ulong attackerClientId = ulong.MaxValue)
    {
        BaseDamage = baseDamage;
        Attacker = attacker;
        IsMelee = isMelee;
        AttackerClientId = attackerClientId;
    }
}
