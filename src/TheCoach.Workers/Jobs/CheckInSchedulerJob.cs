using TheCoach.Application.CheckIns.Persistence;
using TheCoach.Application.CheckIns.Services;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Workers.Jobs;

public sealed class CheckInSchedulerJob : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<CheckInSchedulerJob> _logger;

    public CheckInSchedulerJob(IServiceProvider services, ILogger<CheckInSchedulerJob> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = NextRunAt(DateTimeOffset.UtcNow);
            _logger.LogInformation("CheckInSchedulerJob next run at {Next}", next);
            await Task.Delay(next - DateTimeOffset.UtcNow, stoppingToken);

            await GenerateResponsesAsync(stoppingToken);
        }
    }

    private static DateTimeOffset NextRunAt(DateTimeOffset now)
    {
        var today7am = new DateTimeOffset(now.Year, now.Month, now.Day, 7, 0, 0, TimeSpan.Zero);
        return now < today7am ? today7am : today7am.AddDays(1);
    }

    private async Task GenerateResponsesAsync(CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<CheckInsDbContext>>();
        var tenant = new NullTenantContext();
        var db = new CheckInsDbContext(options, tenant);
        var svc = new CheckInService(db, tenant);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var created = await svc.GenerateDueResponsesAsync(today, ct);
        _logger.LogInformation("CheckInSchedulerJob created {Count} check-in responses for {Date}", created, today);
    }

    private sealed class NullTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public string TenantSlug => string.Empty;
        public string PlanTier => "free";
        public bool IsSystemAdmin => true;
    }
}
