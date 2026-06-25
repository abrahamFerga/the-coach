using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.AiGeneration.Domain;

public enum DraftType { WorkoutProgram, MealPlan }

public enum DraftStatus { Ready, Accepted, Rejected }

public class AiDraft : TenantScopedEntity
{
    public DraftType DraftType { get; set; }
    public DraftStatus Status { get; set; } = DraftStatus.Ready;
    public Guid CoachId { get; set; }
    public string Prompt { get; set; } = string.Empty;
    public string ContentJson { get; set; } = "{}";
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    public void Accept() => Status = DraftStatus.Accepted;
    public void Reject() => Status = DraftStatus.Rejected;
}
