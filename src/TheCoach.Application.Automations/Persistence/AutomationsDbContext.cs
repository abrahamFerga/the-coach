using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Automations.Domain;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.Foundations.Persistence;

namespace TheCoach.Application.Automations.Persistence;

public class AutomationsDbContext : ApplicationDbContext
{
    public DbSet<AutomationWorkflow> AutomationWorkflows => Set<AutomationWorkflow>();
    public DbSet<AutomationRun> AutomationRuns => Set<AutomationRun>();
    public DbSet<AutomationOutboxItem> AutomationOutboxItems => Set<AutomationOutboxItem>();

    public AutomationsDbContext(DbContextOptions<AutomationsDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AutomationWorkflow>(b =>
        {
            b.ToTable("automation_workflows");
            b.Property(w => w.TriggerEvent).HasConversion<string>();
            b.Property(w => w.StepsJson).HasColumnName("steps");
            b.HasIndex(w => new { w.TenantId, w.TriggerEvent, w.IsEnabled });
        });

        modelBuilder.Entity<AutomationRun>(b =>
        {
            b.ToTable("automation_runs");
            b.Property(r => r.Status).HasConversion<string>();
            b.HasIndex(r => new { r.TenantId, r.WorkflowId, r.TriggeredAt });
        });

        modelBuilder.Entity<AutomationOutboxItem>(b =>
        {
            b.ToTable("automation_outbox");
            b.Property(i => i.ActionType).HasConversion<string>();
            b.HasIndex(i => new { i.TenantId, i.ScheduledFor })
                .HasFilter("processed_at IS NULL");
        });
    }
}
