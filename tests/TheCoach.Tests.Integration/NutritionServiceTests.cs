using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.HealthTracking.Domain;
using TheCoach.Application.HealthTracking.Persistence;
using TheCoach.Application.HealthTracking.Services;

namespace TheCoach.Tests.Integration;

public class NutritionServiceTests
{
    private static (HealthTrackingDbContext db, NutritionService svc, FoodDatabaseService foodSvc) Build(Guid tenantId, string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<HealthTrackingDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        var tenant = new StubTenantContext(tenantId);
        var db = new HealthTrackingDbContext(options, tenant);
        return (db, new NutritionService(db, tenant), new FoodDatabaseService(db, tenant));
    }

    [Fact]
    public async Task SetTarget_creates_target_scoped_to_tenant()
    {
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var (_, svc, _) = Build(tenantId);

        var target = await svc.SetTargetAsync(clientId, coachId, 2200, 180, 220, 70);

        target.TenantId.Should().Be(tenantId);
        target.CalorieTarget.Should().Be(2200);
        target.ProteinGrams.Should().Be(180);
    }

    [Fact]
    public async Task LogMeal_and_GetDailySummary_computes_macros_correctly()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (db, svc, foodSvc) = Build(tenantId, dbName);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Seed a food item
        var chicken = new FoodItem
        {
            Name = "Chicken Breast",
            CaloriesPer100g = 165m,
            ProteinPer100g = 31m,
            CarbPer100g = 0m,
            FatPer100g = 3.6m,
            Source = FoodSource.Custom,
            TenantId = tenantId
        };
        db.FoodItems.Add(chicken);
        await db.SaveChangesAsync();

        await svc.SetTargetAsync(clientId, coachId, 2000, 150, 200, 60);
        await svc.LogMealAsync(clientId, today, [new LoggedFoodItem(chicken.Id, 200m)]);

        var (_, svc2, _) = Build(tenantId, dbName);
        var summary = await svc2.GetDailySummaryAsync(clientId, today);

        summary.Should().NotBeNull();
        summary!.Consumed.Calories.Should().Be(330m);
        summary.Consumed.ProteinGrams.Should().Be(62m);
        summary.Target.Should().NotBeNull();
        summary.Target!.CalorieTarget.Should().Be(2000);
    }

    [Fact]
    public async Task LogMeal_upserts_for_same_day()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (db, svc, _) = Build(tenantId, dbName);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var foodId = Guid.NewGuid();

        var food = new FoodItem
        {
            Id = foodId, Name = "Rice", CaloriesPer100g = 130m,
            ProteinPer100g = 2.7m, CarbPer100g = 28m, FatPer100g = 0.3m,
            Source = FoodSource.Custom, TenantId = tenantId
        };
        db.FoodItems.Add(food);
        await db.SaveChangesAsync();

        await svc.LogMealAsync(clientId, today, [new LoggedFoodItem(foodId, 100m)]);
        await svc.LogMealAsync(clientId, today, [new LoggedFoodItem(foodId, 200m)]);

        var (_, svc2, _) = Build(tenantId, dbName);
        var logs = await new HealthTrackingDbContext(
            new DbContextOptionsBuilder<HealthTrackingDbContext>().UseInMemoryDatabase(dbName).Options,
            new StubTenantContext(tenantId)).NutritionLogs.ToListAsync();

        logs.Should().ContainSingle(l => l.ClientId == clientId && l.LogDate == today);
    }

    [Fact]
    public async Task NutritionLogs_are_isolated_by_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var dbName = "nt-isolation";
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        var (dbA, svcA, _) = Build(tenantA, dbName);
        await svcA.LogMealAsync(clientId, today, []);

        var (_, svcB, _) = Build(tenantB, dbName);
        var summary = await svcB.GetDailySummaryAsync(clientId, today);

        summary.Should().BeNull("nutrition logs from other tenants must not be visible");
    }

    private record StubTenantContext(Guid TenantId) : ITenantContext
    {
        public string TenantSlug => "stub";
        public string PlanTier => "pro";
        public bool IsSystemAdmin => false;
    }
}
