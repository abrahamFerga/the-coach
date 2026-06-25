using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheCoach.Application.AthleteAnalytics.Domain;
using TheCoach.Application.AthleteAnalytics.Persistence;
using TheCoach.Application.AthleteAnalytics.Services;
using TheCoach.Application.Coaching.Domain;
using TheCoach.Application.Coaching.Persistence;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Tests.Integration;

public class AthleteAnalyticsServiceTests
{
    private static (AthleteAnalyticsDbContext analyticsDb, CoachingDbContext coachingDb, AthleteAnalyticsService svc)
        Build(Guid tenantId, string? dbName = null)
    {
        var id = dbName ?? Guid.NewGuid().ToString();
        var tenant = new StubTenantContext(tenantId);

        var analyticsOpts = new DbContextOptionsBuilder<AthleteAnalyticsDbContext>()
            .UseInMemoryDatabase(id + "-analytics")
            .Options;
        var coachingOpts = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(id + "-coaching")
            .Options;

        var analyticsDb = new AthleteAnalyticsDbContext(analyticsOpts, tenant);
        var coachingDb = new CoachingDbContext(coachingOpts, tenant);

        return (analyticsDb, coachingDb, new AthleteAnalyticsService(analyticsDb, coachingDb, tenant));
    }

    [Fact]
    public async Task LogReadiness_creates_entry_with_score_and_hrv()
    {
        var tenantId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var (_, _, svc) = Build(tenantId);

        var entry = await svc.LogReadinessAsync(athleteId, new DateOnly(2026, 6, 24), 8, 45.5m, "Felt good");

        entry.ReadinessScore.Should().Be(8);
        entry.HrvMs.Should().Be(45.5m);
        entry.TenantId.Should().Be(tenantId);
    }

    [Fact]
    public async Task LogReadiness_upserts_same_day()
    {
        var tenantId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var date = new DateOnly(2026, 6, 24);

        var (_, _, svc) = Build(tenantId, dbName);
        await svc.LogReadinessAsync(athleteId, date, 6, null, null);

        var (analyticsDb, coachingDb, svc2) = Build(tenantId, dbName);
        var updated = await svc2.LogReadinessAsync(athleteId, date, 9, 50m, null);

        var all = await analyticsDb.ReadinessEntries
            .Where(r => r.AthleteId == athleteId)
            .ToListAsync();
        all.Should().HaveCount(1, "upsert must not create a duplicate");
        all[0].ReadinessScore.Should().Be(9);
        all[0].HrvMs.Should().Be(50m);
    }

    [Fact]
    public async Task LogReadiness_throws_on_out_of_range_score()
    {
        var (_, _, svc) = Build(Guid.NewGuid());
        var act = async () => await svc.LogReadinessAsync(Guid.NewGuid(), DateOnly.FromDateTime(DateTime.Today), 11);
        await act.Should().ThrowAsync<ArgumentOutOfRangeException>();
    }

    [Fact]
    public async Task GetTrainingLoad_returns_zero_with_no_logs()
    {
        var (_, _, svc) = Build(Guid.NewGuid());
        var result = await svc.GetTrainingLoadAsync(Guid.NewGuid());

        result.Atl.Should().Be(0);
        result.Ctl.Should().Be(0);
        result.Tsb.Should().Be(0);
    }

    [Fact]
    public async Task GetTrainingLoad_reflects_workout_sessions()
    {
        var tenantId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (_, coachingDb, svc) = Build(tenantId, dbName);

        var exerciseId = Guid.NewGuid();
        coachingDb.WorkoutLogs.Add(new WorkoutLog
        {
            TenantId = tenantId,
            ClientId = athleteId,
            LoggedAt = DateTimeOffset.UtcNow.AddDays(-1)
        });
        // set sets with RPE manually
        var log = coachingDb.WorkoutLogs.Local.First();
        log.SetSets([new SetEntry(exerciseId, 1, 10, 100m, 7m), new SetEntry(exerciseId, 2, 10, 100m, 7m)]);
        await coachingDb.SaveChangesAsync();

        var result = await svc.GetTrainingLoadAsync(athleteId);

        result.Atl.Should().BeGreaterThan(0, "ATL should increase after a workout");
    }

    [Fact]
    public async Task CreatePeriodizationPlan_stores_mesocycles()
    {
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var (_, _, svc) = Build(tenantId);

        var mesocycles = new[]
        {
            new Mesocycle(MesocycleType.Hypertrophy, 4, 300, 500),
            new Mesocycle(MesocycleType.Strength, 4, 400, 600),
            new Mesocycle(MesocycleType.Peaking, 2, 200, 400),
            new Mesocycle(MesocycleType.Deload, 1, 100, 200)
        };

        var plan = await svc.CreatePeriodizationPlanAsync(
            coachId, athleteId, "Competition Prep", new DateOnly(2026, 9, 1), mesocycles);

        plan.Id.Should().NotBeEmpty();
        plan.TenantId.Should().Be(tenantId);
        plan.GetMesocycles().Should().HaveCount(4);
        plan.GetMesocycles()[0].Type.Should().Be(MesocycleType.Hypertrophy);
    }

    [Fact]
    public async Task LogDrillResult_and_GetDrillTrend_round_trip()
    {
        var tenantId = Guid.NewGuid();
        var athleteId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var drill = "40-yd dash";
        var metric = "Time (s)";

        var (_, _, svc) = Build(tenantId, dbName);
        await svc.LogDrillResultAsync(athleteId, drill, metric, 4.55m, DateTimeOffset.UtcNow.AddDays(-10));
        await svc.LogDrillResultAsync(athleteId, drill, metric, 4.48m, DateTimeOffset.UtcNow.AddDays(-3));

        var (_, _, svc2) = Build(tenantId, dbName);
        var trend = await svc2.GetDrillTrendAsync(athleteId, drill, metric, 90);

        trend.Should().HaveCount(2);
        trend[0].Value.Should().Be(4.55m);
        trend[1].Value.Should().Be(4.48m);
    }

    [Fact]
    public async Task Analytics_are_isolated_by_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dbName = "analytics-isolation";
        var athleteId = Guid.NewGuid();

        var (_, _, svcA) = Build(tenantA, dbName);
        await svcA.LogDrillResultAsync(athleteId, "Vertical", "cm", 65m);

        var (_, _, svcB) = Build(tenantB, dbName);
        var trend = await svcB.GetDrillTrendAsync(athleteId, "Vertical", "cm");

        trend.Should().BeEmpty("tenant B must not see tenant A's drill results");
    }

    private record StubTenantContext(Guid TenantId) : ITenantContext
    {
        public string TenantSlug => "stub";
        public string PlanTier => "pro";
        public bool IsSystemAdmin => false;
    }
}
