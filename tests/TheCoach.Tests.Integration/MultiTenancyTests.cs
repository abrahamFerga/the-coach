using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Coaching.Domain;
using TheCoach.Application.Coaching.Persistence;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Tests.Integration;

public class MultiTenancyTests
{
    private static CoachingDbContext BuildDb(ITenantContext tenant)
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CoachingDbContext(options, tenant);
    }

    private static StubTenantContext Tenant(Guid id) => new(id);

    [Fact]
    public async Task Programs_are_isolated_by_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        // Seed a program in tenant A's context
        await using (var dbA = BuildDb(Tenant(tenantA)))
        {
            dbA.Programs.Add(new Program
            {
                Name = "Tenant A Program",
                TenantId = tenantA,
                CreatedByCoachId = Guid.NewGuid()
            });
            await dbA.SaveChangesAsync();
        }

        // Tenant B should not see tenant A's programs
        await using var dbB = BuildDb(Tenant(tenantB));
        var programs = await dbB.Programs.ToListAsync();
        programs.Should().BeEmpty("tenant B must not see tenant A's programs");
    }

    [Fact]
    public async Task WorkoutLogs_are_isolated_by_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        await using (var dbA = BuildDb(Tenant(tenantA)))
        {
            var log = new WorkoutLog { ClientId = clientId, TenantId = tenantA };
            log.SetSets([new SetEntry(Guid.NewGuid(), 1, 10, 100m, 8m)]);
            dbA.WorkoutLogs.Add(log);
            await dbA.SaveChangesAsync();
        }

        await using var dbB = BuildDb(Tenant(tenantB));
        var logs = await dbB.WorkoutLogs.Where(l => l.ClientId == clientId).ToListAsync();
        logs.Should().BeEmpty("cross-tenant workout logs must not be visible");
    }

    [Fact]
    public async Task Exercise_global_library_is_visible_to_all_tenants()
    {
        var tenantId = Guid.NewGuid();
        var sharedDb = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase("shared-seed")
            .Options;

        await using (var dbSeed = new CoachingDbContext(sharedDb, Tenant(Guid.Empty)))
        {
            dbSeed.Exercises.AddRange(ExerciseSeed.GlobalExercises.Take(5));
            await dbSeed.SaveChangesAsync();
        }

        await using var dbTenant = new CoachingDbContext(sharedDb, Tenant(tenantId));
        var exercises = await dbTenant.Exercises.ToListAsync();
        exercises.Should().NotBeEmpty("global exercises (TenantId = empty) should be visible to any tenant");
    }

    [Fact]
    public async Task Tenant_custom_exercise_not_visible_to_other_tenants()
    {
        var db = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase("custom-exercise")
            .Options;
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();

        await using (var dbA = new CoachingDbContext(db, Tenant(tenantA)))
        {
            dbA.Exercises.Add(new Exercise { Name = "Secret Move", MuscleGroup = "Core", TenantId = tenantA });
            await dbA.SaveChangesAsync();
        }

        await using var dbB = new CoachingDbContext(db, Tenant(tenantB));
        var ex = await dbB.Exercises.FirstOrDefaultAsync(e => e.Name == "Secret Move");
        ex.Should().BeNull("custom exercises from other tenants must not be visible");
    }

    private record StubTenantContext(Guid TenantId) : ITenantContext
    {
        public string TenantSlug => "stub";
        public string PlanTier => "pro";
        public bool IsSystemAdmin => false;
    }
}
