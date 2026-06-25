using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Billing.Domain;
using TheCoach.Application.Billing.Persistence;

namespace TheCoach.Application.Billing.Services;

public class BillingService
{
    private readonly BillingDbContext _db;
    private readonly IStripeGateway _stripe;
    private static readonly TimeSpan TrialDuration = TimeSpan.FromDays(14);

    public BillingService(BillingDbContext db, IStripeGateway stripe)
    {
        _db = db;
        _stripe = stripe;
    }

    public async Task<TenantSubscription> GetOrCreateSubscriptionAsync(
        Guid tenantId,
        CancellationToken ct = default)
    {
        var sub = await _db.TenantSubscriptions.IgnoreQueryFilters()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);

        if (sub is not null) return sub;

        sub = new TenantSubscription
        {
            TenantId = tenantId,
            Status = SubscriptionStatus.Trial,
            TrialEndsAt = DateTimeOffset.UtcNow.Add(TrialDuration)
        };
        _db.TenantSubscriptions.Add(sub);
        await _db.SaveChangesAsync(ct);
        return sub;
    }

    public async Task<string> GetBillingPortalUrlAsync(
        Guid tenantId,
        string returnUrl,
        CancellationToken ct = default)
    {
        var sub = await GetOrCreateSubscriptionAsync(tenantId, ct);

        if (sub.StripeCustomerId is null)
        {
            var customerId = await _stripe.CreateCustomerAsync(tenantId.ToString(), ct);
            sub.StripeCustomerId = customerId;
            await _db.SaveChangesAsync(ct);
        }

        return await _stripe.CreateBillingPortalSessionAsync(sub.StripeCustomerId, returnUrl, ct);
    }

    public async Task<bool> HandleWebhookAsync(
        string stripeEventId,
        string eventType,
        Guid? tenantId,
        string? stripeSubscriptionId,
        DateTimeOffset? periodEnd,
        CancellationToken ct = default)
    {
        var alreadyProcessed = await _db.WebhookEvents.IgnoreQueryFilters()
            .AnyAsync(e => e.StripeEventId == stripeEventId, ct);

        if (alreadyProcessed) return false;

        if (tenantId.HasValue)
        {
            var sub = await _db.TenantSubscriptions.IgnoreQueryFilters()
                .FirstOrDefaultAsync(s => s.TenantId == tenantId.Value, ct);

            if (sub is not null)
            {
                switch (eventType)
                {
                    case "invoice.payment_succeeded":
                        if (stripeSubscriptionId is not null && periodEnd.HasValue)
                            sub.Activate(stripeSubscriptionId, periodEnd.Value);
                        break;

                    case "invoice.payment_failed":
                        var daysPastDue = sub.CurrentPeriodEnd.HasValue
                            ? (DateTimeOffset.UtcNow - sub.CurrentPeriodEnd.Value).Days
                            : 0;
                        if (daysPastDue >= 14 && sub.StripeSubscriptionId is not null)
                        {
                            sub.Suspend();
                            await _stripe.SuspendSubscriptionAsync(sub.StripeSubscriptionId, ct);
                        }
                        else
                        {
                            sub.MarkPastDue();
                        }
                        break;

                    case "customer.subscription.deleted":
                        sub.Cancel();
                        break;
                }
            }
        }

        _db.WebhookEvents.Add(new WebhookEvent
        {
            StripeEventId = stripeEventId,
            EventType = eventType
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<TenantSubscription?> GetSubscriptionAsync(Guid tenantId, CancellationToken ct = default) =>
        await _db.TenantSubscriptions.IgnoreQueryFilters()
            .AsNoTracking()
            .FirstOrDefaultAsync(s => s.TenantId == tenantId, ct);
}
