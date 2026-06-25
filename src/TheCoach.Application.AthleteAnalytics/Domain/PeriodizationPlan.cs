using System.Text.Json;
using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.AthleteAnalytics.Domain;

public enum MesocycleType { Hypertrophy, Strength, Peaking, Deload }

public record Mesocycle(
    MesocycleType Type,
    int WeekCount,
    decimal TargetLoadMin,
    decimal TargetLoadMax);

public class PeriodizationPlan : TenantScopedEntity
{
    public Guid CoachId { get; set; }
    public Guid AthleteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public DateOnly StartsOn { get; set; }
    public string MesocyclesJson { get; private set; } = "[]";

    public List<Mesocycle> GetMesocycles() =>
        JsonSerializer.Deserialize<List<Mesocycle>>(MesocyclesJson) ?? [];

    public void SetMesocycles(IEnumerable<Mesocycle> mesocycles) =>
        MesocyclesJson = JsonSerializer.Serialize(mesocycles.ToList());
}
