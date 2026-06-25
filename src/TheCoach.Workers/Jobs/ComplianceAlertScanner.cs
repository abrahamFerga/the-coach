using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Coaching.Persistence;
using TheCoach.Application.Coaching.Services;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Workers.Jobs;

public sealed class ComplianceAlertScanner : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<ComplianceAlertScanner> _logger;

    public ComplianceAlertScanner(IServiceProvider services, ILogger<ComplianceAlertScanner> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var next = NextRunAt(DateTimeOffset.UtcNow);
            _logger.LogInformation("ComplianceAlertScanner next run at {Next}", next);
            await Task.Delay(next - DateTimeOffset.UtcNow, stoppingToken);

            await ScanAllTenantsAsync(stoppingToken);
        }
    }

    private static DateTimeOffset NextRunAt(DateTimeOffset now)
    {
        var today6am = new DateTimeOffset(now.Year, now.Month, now.Day, 6, 0, 0, TimeSpan.Zero);
        return now < today6am ? today6am : today6am.AddDays(1);
    }

    private async Task ScanAllTenantsAsync(CancellationToken ct)
    {
        await using var scope = _services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<CoachingDbContext>();

        var tenantIds = await db.ProgramAssignments
            .IgnoreQueryFilters()
            .Select(a => a.TenantId)
            .Distinct()
            .ToListAsync(ct);

        var totalRaised = 0;
        foreach (var tenantId in tenantIds)
        {
            var tenant = new SystemScanTenantContext(tenantId);
            var tenantDb = CreateTenantDb(scope, tenantId);
            var svc = new ComplianceService(tenantDb, tenant);
            totalRaised += await svc.ScanAndRaiseAlertsAsync(ct);
        }

        _logger.LogInformation("ComplianceAlertScanner raised {Count} alerts across {Tenants} tenants", totalRaised, tenantIds.Count);
    }

    private static CoachingDbContext CreateTenantDb(AsyncServiceScope scope, Guid tenantId)
    {
        var options = scope.ServiceProvider.GetRequiredService<Microsoft.EntityFrameworkCore.DbContextOptions<CoachingDbContext>>();
        return new CoachingDbContext(options, new SystemScanTenantContext(tenantId));
    }

    private record SystemScanTenantContext(Guid TenantId) : ITenantContext
    {
        public string TenantSlug => string.Empty;
        public string PlanTier => "pro";
        public bool IsSystemAdmin => false;
    }
}
