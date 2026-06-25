using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.Automations.Domain;

public class AutomationOutboxItem : TenantScopedEntity
{
    public Guid RunId { get; set; }
    public int StepOrder { get; set; }
    public AutomationActionType ActionType { get; set; }
    public string Payload { get; set; } = string.Empty;
    public DateTimeOffset ScheduledFor { get; set; }
    public DateTimeOffset? ProcessedAt { get; private set; }
    public int RetryCount { get; private set; }
    public string? LastError { get; private set; }

    public bool IsPending => ProcessedAt == null && RetryCount < 5;

    public void MarkProcessed() => ProcessedAt = DateTimeOffset.UtcNow;

    public void RecordFailure(string error)
    {
        RetryCount++;
        LastError = error;
    }

    public DateTimeOffset NextRetryAt() =>
        DateTimeOffset.UtcNow.AddMinutes(Math.Pow(2, RetryCount));
}
