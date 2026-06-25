using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.Foundations.Persistence;
using TheCoach.Application.HealthTracking.Domain;

namespace TheCoach.Application.HealthTracking.Persistence;

public class HealthTrackingDbContext : ApplicationDbContext
{
    public DbSet<FoodItem> FoodItems => Set<FoodItem>();
    public DbSet<NutritionTarget> NutritionTargets => Set<NutritionTarget>();
    public DbSet<NutritionLog> NutritionLogs => Set<NutritionLog>();
    public DbSet<BodyMetric> BodyMetrics => Set<BodyMetric>();

    public HealthTrackingDbContext(DbContextOptions<HealthTrackingDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<FoodItem>(b =>
        {
            b.ToTable("food_items");
            b.HasIndex(f => f.Barcode).IsUnique().HasFilter("barcode IS NOT NULL");
            b.HasIndex(f => new { f.TenantId, f.Name });
        });

        modelBuilder.Entity<NutritionTarget>(b =>
        {
            b.ToTable("nutrition_targets");
            b.HasIndex(t => new { t.TenantId, t.ClientId, t.EffectiveDate });
        });

        modelBuilder.Entity<NutritionLog>(b =>
        {
            b.ToTable("nutrition_logs");
            b.HasIndex(l => new { l.TenantId, l.ClientId, l.LogDate }).IsUnique();
            b.Property(l => l.FoodItemsJson).HasColumnName("food_items");
        });

        modelBuilder.Entity<BodyMetric>(b =>
        {
            b.ToTable("body_metrics");
            b.HasIndex(m => new { m.TenantId, m.ClientId, m.RecordedOn }).IsUnique();
            b.Property(m => m.MeasurementsJson).HasColumnName("measurements");
        });
    }
}
