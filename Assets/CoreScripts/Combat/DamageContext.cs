using UnityEngine;

public struct DamageContext
{
    public ulong AttackerClientId;
    public bool IsCritical;
    public DamageFeedbackMode FeedbackMode;
    /// <summary>
    /// Indica se o dano veio de uma Torre. Torres podem quebrar escudos de inimigos; jogadores nao.
    /// </summary>
    public bool IsFromTower;
    public bool IsSilverBullet;
    public bool IsAreaDamage;
    public bool HasSourcePosition;
    public Vector3 SourcePosition;

    public DamageContext(
        ulong attackerClientId,
        bool isCritical,
        DamageFeedbackMode feedbackMode = DamageFeedbackMode.InstigatorOnly,
        bool isFromTower = false,
        bool isSilverBullet = false,
        bool isAreaDamage = false,
        Vector3? sourcePosition = null)
    {
        AttackerClientId = attackerClientId;
        IsCritical = isCritical;
        FeedbackMode = feedbackMode;
        IsFromTower = isFromTower;
        IsSilverBullet = isSilverBullet;
        IsAreaDamage = isAreaDamage;
        HasSourcePosition = sourcePosition.HasValue;
        SourcePosition = sourcePosition.GetValueOrDefault();
    }
}
