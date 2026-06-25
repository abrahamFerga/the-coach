using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.HealthTracking.Persistence;
using TheCoach.Application.HealthTracking.Services;

namespace TheCoach.Tests.Integration;

public class BodyMetricTests
{
    private static (HealthTrackingDbContext db, BodyMetricService svc) Build(Guid tenantId, string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<HealthTrackingDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        var tenant = new StubTenantContext(tenantId);
        var db = new HealthTrackingDbContext(options, tenant);
        return (db, new BodyMetricService(db, tenant));
    }

    [Fact]
    public async Task LogAsync_creates_metric_scoped_to_tenant()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var (_, svc) = Build(tenantId);

        var metric = await svc.LogAsync(clientId, date, 85.5m, 18.2m, null, null);

        metric.TenantId.Should().Be(tenantId);
        metric.ClientId.Should().Be(clientId);
        metric.WeightKg.Should().Be(85.5m);
        metric.BodyFatPercent.Should().Be(18.2m);
    }

    [Fact]
    public async Task LogAsync_upserts_for_same_date()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var (_, svc) = Build(tenantId, dbName);
        await svc.LogAsync(clientId, date, 86m, null, null, null);
        await svc.LogAsync(clientId, date, 85m, 17.5m, null, null);

        var (db2, _) = Build(tenantId, dbName);
        var all = await db2.BodyMetrics.ToListAsync();
        all.Should().ContainSingle(m => m.ClientId == clientId && m.RecordedOn == date);
        all[0].WeightKg.Should().Be(85m);
        all[0].BodyFatPercent.Should().Be(17.5m);
    }

    [Fact]
    public async Task GetTrendAsync_filters_by_window()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var (_, svc) = Build(tenantId, dbName);
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        await svc.LogAsync(clientId, today.AddDays(-10), 88m, null, null, null);
        await svc.LogAsync(clientId, today.AddDays(-5), 87m, null, null, null);
        await svc.LogAsync(clientId, today, 86m, null, null, null);

        var (_, svc2) = Build(tenantId, dbName);
        var window = new MetricWindow(today.AddDays(-6), today);
        var trend = await svc2.GetTrendAsync(clientId, window);

        trend.Should().HaveCount(2);
        trend[0].RecordedOn.Should().Be(today.AddDays(-5));
        trend[1].RecordedOn.Should().Be(today);
    }

    [Fact]
    public async Task GetPersonalBestsAsync_returns_min_weight_and_body_fat()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var (_, svc) = Build(tenantId, dbName);
        await svc.LogAsync(clientId, today.AddDays(-10), 90m, 22m, null, null);
        await svc.LogAsync(clientId, today.AddDays(-5), 87m, 20m, null, null);
        await svc.LogAsync(clientId, today, 89m, 21m, null, null);

        var (_, svc2) = Build(tenantId, dbName);
        var pbs = await svc2.GetPersonalBestsAsync(clientId);

        pbs.Should().Contain(p => p.Metric == "weight_kg" && p.Value == 87m);
        pbs.Should().Contain(p => p.Metric == "body_fat_pct" && p.Value == 20m);
    }

    [Fact]
    public async Task GetPersonalBestsAsync_includes_named_measurements()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var (_, svc) = Build(tenantId, dbName);
        await svc.LogAsync(clientId, today.AddDays(-5), null, null, new Dictionary<string, decimal> { ["waist_cm"] = 85m }, null);
        await svc.LogAsync(clientId, today, null, null, new Dictionary<string, decimal> { ["waist_cm"] = 82m }, null);

        var (_, svc2) = Build(tenantId, dbName);
        var pbs = await svc2.GetPersonalBestsAsync(clientId);

        pbs.Should().Contain(p => p.Metric == "waist_cm" && p.Value == 82m);
    }

    [Fact]
    public async Task BodyMetrics_are_isolated_by_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var dbName = "bm-isolation";
        var date = DateOnly.FromDateTime(DateTime.UtcNow);

        var (_, svcA) = Build(tenantA, dbName);
        await svcA.LogAsync(clientId, date, 80m, null, null, null);

        var (_, svcB) = Build(tenantB, dbName);
        var trend = await svcB.GetTrendAsync(clientId, new MetricWindow(date.AddDays(-1), date.AddDays(1)));

        trend.Should().BeEmpty("body metrics from other tenants must not be visible");
    }

    private record StubTenantContext(Guid TenantId) : ITenantContext
    {
        public string TenantSlug => "stub";
        public string PlanTier => "pro";
        public bool IsSystemAdmin => false;
    }
}
