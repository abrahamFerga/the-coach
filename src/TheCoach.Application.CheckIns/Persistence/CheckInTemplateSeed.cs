using TheCoach.Application.CheckIns.Domain;

namespace TheCoach.Application.CheckIns.Persistence;

public static class CheckInTemplateSeed
{
    public static CheckInTemplate WeeklyCheckIn()
    {
        var template = new CheckInTemplate
        {
            Name = "Weekly Check-In",
            Description = "Default weekly athlete check-in covering recovery and motivation.",
            RecurrenceDayOfWeek = DayOfWeek.Monday,
            IsBuiltIn = true,
            TenantId = Guid.Empty
        };

        template.SetQuestions([
            new CheckInQuestion(Guid.Parse("11111111-0000-7000-8000-000000000001"), "Sleep quality", QuestionType.Scale, 1),
            new CheckInQuestion(Guid.Parse("11111111-0000-7000-8000-000000000002"), "Energy level", QuestionType.Scale, 2),
            new CheckInQuestion(Guid.Parse("11111111-0000-7000-8000-000000000003"), "Stress level", QuestionType.Scale, 3),
            new CheckInQuestion(Guid.Parse("11111111-0000-7000-8000-000000000004"), "Motivation", QuestionType.Scale, 4),
            new CheckInQuestion(Guid.Parse("11111111-0000-7000-8000-000000000005"), "Additional notes", QuestionType.Text, 5),
        ]);

        return template;
    }
}
