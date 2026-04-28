public struct DamageContext
{
    public ulong AttackerClientId;
    public bool IsCritical;
    public DamageFeedbackMode FeedbackMode;

    public DamageContext(ulong attackerClientId, bool isCritical, DamageFeedbackMode feedbackMode = DamageFeedbackMode.InstigatorOnly)
    {
        AttackerClientId = attackerClientId;
        IsCritical = isCritical;
        FeedbackMode = feedbackMode;
    }
}
