using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.Billing.Domain;

public enum PlanTier { Member, Trainer, Studio, Gym }

public enum SubscriptionStatus { Trial, Active, PastDue, Suspended, Cancelled }

public class TenantSubscription : EntityBase
{
    public Guid TenantId { get; set; }
    public PlanTier PlanTier { get; set; } = PlanTier.Member;
    public SubscriptionStatus Status { get; set; } = SubscriptionStatus.Trial;
    public string? StripeCustomerId { get; set; }
    public string? StripeSubscriptionId { get; set; }
    public DateTimeOffset? TrialEndsAt { get; set; }
    public DateTimeOffset? CurrentPeriodEnd { get; set; }

    public void Activate(string stripeSubscriptionId, DateTimeOffset periodEnd)
    {
        StripeSubscriptionId = stripeSubscriptionId;
        Status = SubscriptionStatus.Active;
        CurrentPeriodEnd = periodEnd;
    }

    public void MarkPastDue() => Status = SubscriptionStatus.PastDue;

    public void Suspend() => Status = SubscriptionStatus.Suspended;

    public void Cancel() => Status = SubscriptionStatus.Cancelled;
}
