using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Coaching.Persistence;
using TheCoach.Application.Coaching.Services;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Tests.Integration;

public class ProgramServiceTests
{
    private static (CoachingDbContext db, ProgramService svc) Build(Guid tenantId)
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        var tenant = new StubTenantContext(tenantId);
        var db = new CoachingDbContext(options, tenant);
        var svc = new ProgramService(db, tenant);
        return (db, svc);
    }

    [Fact]
    public async Task CreateProgram_sets_tenant_and_returns_id()
    {
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (_, svc) = Build(tenantId);

        var program = await svc.CreateAsync("5/3/1", false, coachId);

        program.Id.Should().NotBeEmpty();
        program.TenantId.Should().Be(tenantId);
        program.Name.Should().Be("5/3/1");
    }

    [Fact]
    public async Task AddBlock_then_AddWorkout_then_AddExercise_builds_hierarchy()
    {
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (db, svc) = Build(tenantId);

        var program = await svc.CreateAsync("Hypertrophy Block", false, coachId);
        var block = await svc.AddBlockAsync(program.Id, "Week 1", 1);
        var workout = await svc.AddWorkoutAsync(block.Id, "Push A", DayOfWeek.Monday);
        var exerciseId = Guid.NewGuid();
        await svc.AddExerciseToWorkoutAsync(workout.Id, exerciseId, 4, 8, 80m, 8m, 0);

        var loaded = await svc.GetAsync(program.Id);
        loaded.Should().NotBeNull();
        loaded!.Blocks.Should().ContainSingle(b => b.Name == "Week 1");
        loaded.Blocks[0].Workouts.Should().ContainSingle(w => w.Name == "Push A");
        loaded.Blocks[0].Workouts[0].Exercises.Should().ContainSingle(e => e.ExerciseId == exerciseId);
    }

    [Fact]
    public async Task GetAsync_for_other_tenant_returns_null()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var sharedOptions = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase("cross-tenant-get")
            .Options;

        var tenantACtx = new StubTenantContext(tenantA);
        await using var dbA = new CoachingDbContext(sharedOptions, tenantACtx);
        var svcA = new ProgramService(dbA, tenantACtx);
        var program = await svcA.CreateAsync("Private Program", false, Guid.NewGuid());

        var tenantBCtx = new StubTenantContext(tenantB);
        await using var dbB = new CoachingDbContext(sharedOptions, tenantBCtx);
        var svcB = new ProgramService(dbB, tenantBCtx);
        var result = await svcB.GetAsync(program.Id);

        result.Should().BeNull("programs from other tenants must not be accessible");
    }

    private record StubTenantContext(Guid TenantId) : ITenantContext
    {
        public string TenantSlug => "stub";
        public string PlanTier => "pro";
        public bool IsSystemAdmin => false;
    }
}
