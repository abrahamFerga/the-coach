using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.Automations.Domain;

public enum AutomationRunStatus { Running, Completed, Failed }

public class AutomationRun : TenantScopedEntity
{
    public Guid WorkflowId { get; set; }
    public Guid ClientId { get; set; }
    public DateTimeOffset TriggeredAt { get; set; } = DateTimeOffset.UtcNow;
    public AutomationRunStatus Status { get; set; } = AutomationRunStatus.Running;
    public int StepsTotal { get; set; }
    public int StepsCompleted { get; set; }

    public void RecordStepCompleted()
    {
        StepsCompleted++;
        if (StepsCompleted >= StepsTotal)
            Status = AutomationRunStatus.Completed;
    }

    public void Fail() => Status = AutomationRunStatus.Failed;
}
