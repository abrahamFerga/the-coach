using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.HealthTracking.Domain;

public class NutritionTarget : TenantScopedEntity
{
    public Guid ClientId { get; set; }
    public Guid SetByCoachId { get; set; }
    public int CalorieTarget { get; set; }
    public int ProteinGrams { get; set; }
    public int CarbGrams { get; set; }
    public int FatGrams { get; set; }
    public DateOnly EffectiveDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
}
