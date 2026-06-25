using TheCoach.Application.Automations.Domain;

namespace TheCoach.Application.Automations.Services;

public interface IAutomationActionDispatcher
{
    Task DispatchAsync(AutomationOutboxItem item, CancellationToken ct = default);
}
