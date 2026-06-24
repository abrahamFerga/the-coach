using System.Text.Json;
using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.Automations.Domain;

public enum AutomationTrigger
{
    ClientEnrolled,
    CheckInSubmitted,
    ProgramCompleted,
    ComplianceAlertFired
}

public enum AutomationActionType { SendMessage, AssignProgram, CreateCheckIn }

public record AutomationStep(
    int Order,
    AutomationActionType ActionType,
    int DelayDays,
    string Payload);

public class AutomationWorkflow : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public AutomationTrigger TriggerEvent { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string StepsJson { get; private set; } = "[]";

    public List<AutomationStep> GetSteps() =>
        JsonSerializer.Deserialize<List<AutomationStep>>(StepsJson) ?? [];

    public void SetSteps(IEnumerable<AutomationStep> steps) =>
        StepsJson = JsonSerializer.Serialize(steps.OrderBy(s => s.Order).ToList());

    public void Enable() => IsEnabled = true;
    public void Disable() => IsEnabled = false;
}
