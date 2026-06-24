using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.Coaching.Domain;

public class ComplianceAlert : TenantScopedEntity
{
    public Guid CoachId { get; set; }
    public Guid ClientId { get; set; }
    public DateTimeOffset TriggeredAt { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? AcknowledgedAt { get; private set; }
    public Guid? AcknowledgedByCoachId { get; private set; }
    public bool IsAcknowledged => AcknowledgedAt.HasValue;

    public void Acknowledge(Guid byCoachId)
    {
        AcknowledgedAt = DateTimeOffset.UtcNow;
        AcknowledgedByCoachId = byCoachId;
    }
}
