using System.Text.Json;
using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.CheckIns.Domain;

public class CheckInTemplate : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string QuestionsJson { get; private set; } = "[]";
    public DayOfWeek? RecurrenceDayOfWeek { get; set; }
    public bool IsBuiltIn { get; set; }

    public List<CheckInQuestion> GetQuestions() =>
        JsonSerializer.Deserialize<List<CheckInQuestion>>(QuestionsJson) ?? [];

    public void SetQuestions(IEnumerable<CheckInQuestion> questions) =>
        QuestionsJson = JsonSerializer.Serialize(questions.ToList());
}
