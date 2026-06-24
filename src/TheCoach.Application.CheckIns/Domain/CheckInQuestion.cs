namespace TheCoach.Application.CheckIns.Domain;

public enum QuestionType { Scale, MultipleChoice, Text }

public record CheckInQuestion(
    Guid Id,
    string Text,
    QuestionType Type,
    int Order,
    string[]? Options = null);
