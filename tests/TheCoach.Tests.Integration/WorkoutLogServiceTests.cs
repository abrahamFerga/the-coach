using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Coaching.Domain;
using TheCoach.Application.Coaching.Persistence;
using TheCoach.Application.Coaching.Services;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Tests.Integration;

public class WorkoutLogServiceTests
{
    private static (CoachingDbContext db, WorkoutLogService svc) Build(Guid tenantId, string dbName)
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var tenant = new StubTenantContext(tenantId);
        var db = new CoachingDbContext(options, tenant);
        var svc = new WorkoutLogService(db, tenant);
        return (db, svc);
    }

    [Fact]
    public async Task LogWorkout_persists_sets_and_returns_log()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var (_, svc) = Build(tenantId, Guid.NewGuid().ToString());

        var sets = new[] { new SetEntry(exerciseId, 1, 10, 100m, 8m), new SetEntry(exerciseId, 2, 8, 105m, 9m) };
        var log = await svc.LogAsync(clientId, null, sets);

        log.Id.Should().NotBeEmpty();
        log.GetSets().Should().HaveCount(2);
        log.GetSets()[0].Reps.Should().Be(10);
    }

    [Fact]
    public async Task GetPreviousSets_returns_last_session_sets_for_same_workout()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var workoutId = Guid.NewGuid();
        var exerciseId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (_, svc) = Build(tenantId, dbName);

        var firstSets = new[] { new SetEntry(exerciseId, 1, 10, 80m, null) };
        await svc.LogAsync(clientId, workoutId, firstSets);

        var (_, svc2) = Build(tenantId, dbName);
        var previousSets = await svc2.GetPreviousSessionSetsAsync(clientId, workoutId);

        previousSets.Should().ContainSingle(s => s.Reps == 10 && s.Weight == 80m);
    }

    [Fact]
    public async Task GetClientHistory_does_not_return_other_tenant_logs()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var dbName = "cross-tenant-logs";

        var (_, svcA) = Build(tenantA, dbName);
        await svcA.LogAsync(clientId, null, [new SetEntry(Guid.NewGuid(), 1, 5, 50m, null)]);

        var (_, svcB) = Build(tenantB, dbName);
        var logs = await svcB.GetClientHistoryAsync(clientId);

        logs.Should().BeEmpty("workout logs from another tenant must not leak");
    }

    private record StubTenantContext(Guid TenantId) : ITenantContext
    {
        public string TenantSlug => "stub";
        public string PlanTier => "pro";
        public bool IsSystemAdmin => false;
    }
}
