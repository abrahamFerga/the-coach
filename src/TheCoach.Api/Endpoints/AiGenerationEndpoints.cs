using System.Security.Claims;
using System.Text.Json;
using TheCoach.Application.AiGeneration.Domain;
using TheCoach.Application.AiGeneration.Services;
using TheCoach.Application.Foundations.Auth;

namespace TheCoach.Api.Endpoints;

public static class AiGenerationEndpoints
{
    public static IEndpointRouteBuilder MapAiGenerationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/ai")
            .RequireAuthorization(Policies.AIGenerate)
            .WithTags("AI Generation");

        group.MapPost("/workout-program", async (
            WorkoutProgramRequest req,
            AiGenerationService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var coachId = GetCoachId(user);
            var request = new WorkoutGenerationRequest(
                req.Goal, req.DurationWeeks, req.DaysPerWeek,
                req.Equipment, req.Restrictions, req.AvailableExerciseNames ?? []);
            var draft = await svc.GenerateWorkoutProgramAsync(coachId, request, ct);
            return Results.Ok(MapDraft(draft));
        });

        group.MapPost("/meal-plan", async (
            MealPlanRequest req,
            AiGenerationService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var coachId = GetCoachId(user);
            var request = new MealPlanGenerationRequest(
                req.CalorieTarget, req.ProteinGrams, req.CarbGrams, req.FatGrams,
                req.DietaryRestrictions ?? []);
            var draft = await svc.GenerateMealPlanAsync(coachId, request, ct);
            return Results.Ok(MapDraft(draft));
        });

        group.MapGet("/drafts", async (
            AiGenerationService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var coachId = GetCoachId(user);
            var drafts = await svc.ListDraftsAsync(coachId, ct);
            return Results.Ok(drafts.Select(MapDraft));
        });

        group.MapGet("/drafts/{draftId:guid}", async (
            Guid draftId,
            AiGenerationService svc,
            CancellationToken ct) =>
        {
            var draft = await svc.GetDraftAsync(draftId, ct);
            return draft is null ? Results.NotFound() : Results.Ok(MapDraftWithContent(draft));
        });

        group.MapPost("/drafts/{draftId:guid}/accept", async (
            Guid draftId,
            AiGenerationService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var coachId = GetCoachId(user);
            var draft = await svc.AcceptDraftAsync(draftId, coachId, ct);
            return Results.Ok(new { draft.Id, draft.Status, draft.DraftType, draft.ContentJson });
        });

        group.MapPost("/drafts/{draftId:guid}/reject", async (
            Guid draftId,
            AiGenerationService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var coachId = GetCoachId(user);
            var draft = await svc.RejectDraftAsync(draftId, coachId, ct);
            return Results.Ok(new { draft.Id, draft.Status });
        });

        return app;
    }

    private static Guid GetCoachId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private static object MapDraft(AiDraft d) => new
    {
        d.Id,
        d.DraftType,
        Status = d.Status.ToString(),
        d.Prompt,
        d.GeneratedAt
    };

    private static object MapDraftWithContent(AiDraft d) => new
    {
        d.Id,
        d.DraftType,
        Status = d.Status.ToString(),
        d.Prompt,
        d.GeneratedAt,
        Content = JsonSerializer.Deserialize<object>(d.ContentJson)
    };

    private record WorkoutProgramRequest(
        string Goal,
        int DurationWeeks,
        int DaysPerWeek,
        string[] Equipment,
        string[] Restrictions,
        string[]? AvailableExerciseNames);

    private record MealPlanRequest(
        decimal CalorieTarget,
        decimal ProteinGrams,
        decimal CarbGrams,
        decimal FatGrams,
        string[]? DietaryRestrictions);
}
