using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Billing.Domain;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.Foundations.Persistence;

namespace TheCoach.Application.Billing.Persistence;

public class BillingDbContext : ApplicationDbContext
{
    public DbSet<TenantSubscription> TenantSubscriptions => Set<TenantSubscription>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    public BillingDbContext(DbContextOptions<BillingDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<TenantSubscription>(b =>
        {
            b.ToTable("tenant_subscriptions");
            b.HasIndex(s => s.TenantId).IsUnique();
            b.Property(s => s.PlanTier).HasConversion<string>();
            b.Property(s => s.Status).HasConversion<string>();
        });

        modelBuilder.Entity<WebhookEvent>(b =>
        {
            b.ToTable("billing_webhook_events");
            b.HasIndex(e => e.StripeEventId).IsUnique();
        });
    }
}
