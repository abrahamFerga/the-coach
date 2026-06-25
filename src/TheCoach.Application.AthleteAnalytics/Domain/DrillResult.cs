using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.AthleteAnalytics.Domain;

public class DrillResult : TenantScopedEntity
{
    public Guid AthleteId { get; set; }
    public string DrillName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public DateTimeOffset LoggedAt { get; set; } = DateTimeOffset.UtcNow;
    public string? Notes { get; set; }
}
