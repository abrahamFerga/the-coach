using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.Billing.Domain;

public class WebhookEvent : EntityBase
{
    public string StripeEventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTimeOffset ProcessedAt { get; set; } = DateTimeOffset.UtcNow;
}
