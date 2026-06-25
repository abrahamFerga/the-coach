using System.Security.Claims;
using TheCoach.Application.CheckIns.Domain;
using TheCoach.Application.CheckIns.Services;
using TheCoach.Application.Foundations.Auth;

namespace TheCoach.Api.Endpoints;

public static class CheckInEndpoints
{
    public static IEndpointRouteBuilder MapCheckInEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/checkins")
            .RequireAuthorization()
            .WithTags("CheckIns");

        group.MapPost("/templates", async (
            CreateTemplateRequest req,
            CheckInService svc,
            CancellationToken ct) =>
        {
            var questions = req.Questions.Select((q, i) => new CheckInQuestion(
                Guid.CreateVersion7(), q.Text, q.Type, i + 1, q.Options));
            var template = await svc.CreateTemplateAsync(
                req.Name, req.Description, questions, req.RecurrenceDayOfWeek, ct);
            return Results.Ok(MapTemplate(template));
        }).RequireAuthorization(Policies.CheckInsManage);

        group.MapGet("/templates", async (CheckInService svc, CancellationToken ct) =>
        {
            var templates = await svc.ListTemplatesAsync(ct);
            return Results.Ok(templates.Select(MapTemplate));
        }).RequireAuthorization(Policies.CheckInsManage);

        group.MapPost("/templates/{templateId:guid}/assign", async (
            Guid templateId,
            AssignRequest req,
            CheckInService svc,
            CancellationToken ct) =>
        {
            var assignment = await svc.AssignTemplateAsync(
                templateId, req.ClientId, req.CoachId, req.StartsOn, ct);
            return Results.Ok(new { assignment.Id, assignment.ClientId, assignment.StartsOn });
        }).RequireAuthorization(Policies.CheckInsManage);

        group.MapGet("/responses/due", async (
            ClaimsPrincipal user,
            CheckInService svc,
            CancellationToken ct) =>
        {
            var clientId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var responses = await svc.GetDueResponsesForClientAsync(clientId, ct);
            return Results.Ok(responses.Select(r => new
            {
                r.Id,
                r.AssignmentId,
                r.DueDate,
                r.ExpiresAt
            }));
        }).RequireAuthorization(Policies.CheckInsViewOwn);

        group.MapPost("/responses/{responseId:guid}/submit", async (
            Guid responseId,
            SubmitRequest req,
            ClaimsPrincipal user,
            CheckInService svc,
            CancellationToken ct) =>
        {
            var clientId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var response = await svc.SubmitResponseAsync(responseId, clientId, req.Answers, ct);
            return Results.Ok(new { response.Id, response.SubmittedAt });
        }).RequireAuthorization(Policies.CheckInsViewOwn);

        group.MapGet("/client/{clientId:guid}/trend/{templateId:guid}", async (
            Guid clientId,
            Guid templateId,
            CheckInService svc,
            CancellationToken ct) =>
        {
            var trend = await svc.GetTrendAsync(clientId, templateId, ct);
            return Results.Ok(trend.Select(r => new
            {
                r.DueDate,
                r.SubmittedAt,
                Answers = r.GetAnswers()
            }));
        }).RequireAuthorization(Policies.CheckInsViewAll);

        return app;
    }

    private static object MapTemplate(CheckInTemplate t) => new
    {
        t.Id,
        t.Name,
        t.Description,
        t.RecurrenceDayOfWeek,
        t.IsBuiltIn,
        Questions = t.GetQuestions()
    };

    private record CreateTemplateRequest(
        string Name,
        string? Description,
        DayOfWeek? RecurrenceDayOfWeek,
        QuestionDto[] Questions);

    private record QuestionDto(string Text, QuestionType Type, string[]? Options);

    private record AssignRequest(Guid ClientId, Guid CoachId, DateOnly StartsOn);

    private record SubmitRequest(Dictionary<Guid, string> Answers);
}
