using System.Text.Json;
using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.Coaching.Domain;

public class WorkoutLog : TenantScopedEntity
{
    public Guid ClientId { get; set; }
    public Guid? WorkoutId { get; set; }
    public DateTimeOffset LoggedAt { get; set; } = DateTimeOffset.UtcNow;
    public string SetsJson { get; private set; } = "[]";

    public IReadOnlyList<SetEntry> GetSets() =>
        JsonSerializer.Deserialize<List<SetEntry>>(SetsJson) ?? [];

    public void SetSets(IEnumerable<SetEntry> sets) =>
        SetsJson = JsonSerializer.Serialize(sets);
}

public record SetEntry(Guid ExerciseId, int SetNumber, int Reps, decimal Weight, decimal? Rpe);
