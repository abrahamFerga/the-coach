using Microsoft.EntityFrameworkCore;
using TheCoach.Application.AthleteAnalytics.Domain;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.Foundations.Persistence;

namespace TheCoach.Application.AthleteAnalytics.Persistence;

public class AthleteAnalyticsDbContext : ApplicationDbContext
{
    public DbSet<ReadinessEntry> ReadinessEntries => Set<ReadinessEntry>();
    public DbSet<DrillResult> DrillResults => Set<DrillResult>();
    public DbSet<PeriodizationPlan> PeriodizationPlans => Set<PeriodizationPlan>();

    public AthleteAnalyticsDbContext(
        DbContextOptions<AthleteAnalyticsDbContext> options,
        ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<ReadinessEntry>(b =>
        {
            b.ToTable("readiness_entries");
            b.HasIndex(r => new { r.TenantId, r.AthleteId, r.Date }).IsUnique();
        });

        modelBuilder.Entity<DrillResult>(b =>
        {
            b.ToTable("drill_results");
            b.HasIndex(d => new { d.TenantId, d.AthleteId, d.DrillName, d.LoggedAt });
        });

        modelBuilder.Entity<PeriodizationPlan>(b =>
        {
            b.ToTable("periodization_plans");
            b.Property(p => p.MesocyclesJson).HasColumnName("mesocycles");
            b.HasIndex(p => new { p.TenantId, p.AthleteId });
        });
    }
}
