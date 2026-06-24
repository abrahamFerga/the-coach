using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Billing.Domain;
using TheCoach.Application.Billing.Persistence;
using TheCoach.Application.Billing.Services;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Tests.Integration;

public class BillingServiceTests
{
    private static (BillingDbContext db, BillingService svc) Build(string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        var tenant = new SystemTenantContext();
        var db = new BillingDbContext(options, tenant);
        return (db, new BillingService(db, new NoOpStripeGateway()));
    }

    [Fact]
    public async Task GetOrCreateSubscription_creates_trial_for_new_tenant()
    {
        var tenantId = Guid.NewGuid();
        var (_, svc) = Build();

        var sub = await svc.GetOrCreateSubscriptionAsync(tenantId);

        sub.TenantId.Should().Be(tenantId);
        sub.Status.Should().Be(SubscriptionStatus.Trial);
        sub.TrialEndsAt.Should().NotBeNull();
        sub.TrialEndsAt!.Value.Should().BeCloseTo(DateTimeOffset.UtcNow.AddDays(14), precision: TimeSpan.FromSeconds(5));
    }

    [Fact]
    public async Task GetOrCreateSubscription_is_idempotent()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (_, svc) = Build(dbName);
        await svc.GetOrCreateSubscriptionAsync(tenantId);

        var (_, svc2) = Build(dbName);
        var sub2 = await svc2.GetOrCreateSubscriptionAsync(tenantId);

        var (db, _) = Build(dbName);
        var count = await db.TenantSubscriptions.IgnoreQueryFilters()
            .CountAsync(s => s.TenantId == tenantId);
        count.Should().Be(1, "should not create duplicate subscriptions");
    }

    [Fact]
    public async Task HandleWebhook_payment_succeeded_activates_subscription()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (_, svc) = Build(dbName);
        await svc.GetOrCreateSubscriptionAsync(tenantId);

        var (_, svc2) = Build(dbName);
        var periodEnd = DateTimeOffset.UtcNow.AddMonths(1);
        var processed = await svc2.HandleWebhookAsync(
            "evt_001", "invoice.payment_succeeded", tenantId, "sub_abc", periodEnd);

        processed.Should().BeTrue();

        var (_, svc3) = Build(dbName);
        var sub = await svc3.GetSubscriptionAsync(tenantId);
        sub!.Status.Should().Be(SubscriptionStatus.Active);
        sub.StripeSubscriptionId.Should().Be("sub_abc");
        sub.CurrentPeriodEnd.Should().BeCloseTo(periodEnd, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task HandleWebhook_payment_failed_marks_past_due()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (_, svc) = Build(dbName);
        var sub = await svc.GetOrCreateSubscriptionAsync(tenantId);

        var (_, svc2) = Build(dbName);
        await svc2.HandleWebhookAsync(
            "evt_002", "invoice.payment_failed", tenantId, null, null);

        var (_, svc3) = Build(dbName);
        var updated = await svc3.GetSubscriptionAsync(tenantId);
        updated!.Status.Should().Be(SubscriptionStatus.PastDue);
    }

    [Fact]
    public async Task HandleWebhook_subscription_deleted_cancels()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (_, svc) = Build(dbName);
        await svc.GetOrCreateSubscriptionAsync(tenantId);

        var (_, svc2) = Build(dbName);
        await svc2.HandleWebhookAsync(
            "evt_003", "customer.subscription.deleted", tenantId, null, null);

        var (_, svc3) = Build(dbName);
        var sub = await svc3.GetSubscriptionAsync(tenantId);
        sub!.Status.Should().Be(SubscriptionStatus.Cancelled);
    }

    [Fact]
    public async Task HandleWebhook_is_idempotent()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (_, svc) = Build(dbName);
        await svc.GetOrCreateSubscriptionAsync(tenantId);

        var (_, svc2) = Build(dbName);
        var first = await svc2.HandleWebhookAsync("evt_004", "customer.subscription.deleted", tenantId, null, null);

        var (_, svc3) = Build(dbName);
        var second = await svc3.HandleWebhookAsync("evt_004", "customer.subscription.deleted", tenantId, null, null);

        first.Should().BeTrue();
        second.Should().BeFalse("duplicate webhook must be rejected");
    }

    [Fact]
    public async Task GetBillingPortalUrl_creates_stripe_customer_on_first_call()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (_, svc) = Build(dbName);
        await svc.GetOrCreateSubscriptionAsync(tenantId);

        var (_, svc2) = Build(dbName);
        var url = await svc2.GetBillingPortalUrlAsync(tenantId, "https://app.thecoach.io/billing");

        url.Should().StartWith("https://billing.stripe.com/session/stub_");

        var (_, svc3) = Build(dbName);
        var sub = await svc3.GetSubscriptionAsync(tenantId);
        sub!.StripeCustomerId.Should().NotBeNullOrEmpty();
    }

    private sealed class SystemTenantContext : ITenantContext
    {
        public Guid TenantId => Guid.Empty;
        public string TenantSlug => string.Empty;
        public string PlanTier => "free";
        public bool IsSystemAdmin => true;
    }
}
