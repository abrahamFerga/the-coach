using Microsoft.Extensions.Logging;
using TheCoach.Application.Automations.Domain;

namespace TheCoach.Application.Automations.Services;

public sealed class LoggingActionDispatcher : IAutomationActionDispatcher
{
    private readonly ILogger<LoggingActionDispatcher> _logger;

    public LoggingActionDispatcher(ILogger<LoggingActionDispatcher> logger) => _logger = logger;

    public Task DispatchAsync(AutomationOutboxItem item, CancellationToken ct = default)
    {
        _logger.LogInformation(
            "AutomationAction dispatched: RunId={RunId} Step={Step} Action={Action} Payload={Payload}",
            item.RunId, item.StepOrder, item.ActionType, item.Payload);
        return Task.CompletedTask;
    }
}
