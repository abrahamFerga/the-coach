using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.AthleteAnalytics.Domain;

public class ReadinessEntry : TenantScopedEntity
{
    public Guid AthleteId { get; set; }
    public DateOnly Date { get; set; }
    public int ReadinessScore { get; set; }
    public decimal? HrvMs { get; set; }
    public string? Notes { get; set; }
}
