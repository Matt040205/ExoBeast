public struct DamageContext
{
    public ulong AttackerClientId;
    public bool IsCritical;
    public DamageFeedbackMode FeedbackMode;
    /// <summary>
    /// Indica se o dano veio de uma Torre. Torres podem quebrar escudos de inimigos; jogadores nao.
    /// </summary>
    public bool IsFromTower;

    public DamageContext(ulong attackerClientId, bool isCritical, DamageFeedbackMode feedbackMode = DamageFeedbackMode.InstigatorOnly, bool isFromTower = false)
    {
        AttackerClientId = attackerClientId;
        IsCritical = isCritical;
        FeedbackMode = feedbackMode;
        IsFromTower = isFromTower;
    }
}
