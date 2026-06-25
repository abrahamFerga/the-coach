using System.Text.Json;
using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.CheckIns.Domain;

public class CheckInResponse : TenantScopedEntity
{
    public Guid AssignmentId { get; set; }
    public DateOnly DueDate { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public DateTimeOffset? SubmittedAt { get; private set; }
    public string AnswersJson { get; private set; } = "{}";

    public Dictionary<Guid, string> GetAnswers() =>
        JsonSerializer.Deserialize<Dictionary<Guid, string>>(AnswersJson) ?? [];

    public void Submit(Dictionary<Guid, string> answers)
    {
        AnswersJson = JsonSerializer.Serialize(answers);
        SubmittedAt = DateTimeOffset.UtcNow;
    }
}
