using TheCoach.Application.Automations.Services;

namespace TheCoach.Workers.Jobs;

public sealed class AutomationOutboxProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<AutomationOutboxProcessor> _logger;

    public AutomationOutboxProcessor(
        IServiceScopeFactory scopes,
        ILogger<AutomationOutboxProcessor> logger)
    {
        _scopes = scopes;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _scopes.CreateScope();
                var svc = scope.ServiceProvider.GetRequiredService<AutomationService>();
                var processed = await svc.ProcessOutboxAsync(stoppingToken);
                if (processed > 0)
                    _logger.LogInformation("AutomationOutboxProcessor: dispatched {Count} items", processed);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogError(ex, "AutomationOutboxProcessor error");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
        }
    }
}
