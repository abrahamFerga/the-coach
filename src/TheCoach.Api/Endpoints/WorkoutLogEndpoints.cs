using System.Security.Claims;
using TheCoach.Application.Coaching.Domain;
using TheCoach.Application.Coaching.Services;
using TheCoach.Application.Foundations.Auth;

namespace TheCoach.Api.Endpoints;

public static class WorkoutLogEndpoints
{
    public static IEndpointRouteBuilder MapWorkoutLogEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/workout-logs")
            .RequireAuthorization()
            .WithTags("WorkoutLogs");

        group.MapPost("/", async (
            LogWorkoutRequest req,
            WorkoutLogService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var clientId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var log = await svc.LogAsync(clientId, req.WorkoutId, req.Sets.Select(s =>
                new SetEntry(s.ExerciseId, s.SetNumber, s.Reps, s.Weight, s.Rpe)), ct);
            return Results.Created($"/api/v1/workout-logs/{log.Id}", new { log.Id, log.LoggedAt });
        }).RequireAuthorization(Policies.WorkoutLogsCreate);

        group.MapGet("/client/{clientId:guid}", async (
            Guid clientId,
            WorkoutLogService svc,
            CancellationToken ct) =>
        {
            var logs = await svc.GetClientHistoryAsync(clientId, ct: ct);
            return Results.Ok(logs.Select(l => new
            {
                l.Id,
                l.WorkoutId,
                l.LoggedAt,
                Sets = l.GetSets()
            }));
        }).RequireAuthorization(Policies.ComplianceViewOwn);

        group.MapGet("/workout/{workoutId:guid}/previous-sets", async (
            Guid workoutId,
            WorkoutLogService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var clientId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var sets = await svc.GetPreviousSessionSetsAsync(clientId, workoutId, ct);
            return Results.Ok(sets);
        }).RequireAuthorization(Policies.WorkoutLogsCreate);

        return app;
    }

    private record SetRequest(Guid ExerciseId, int SetNumber, int Reps, decimal Weight, decimal? Rpe);
    private record LogWorkoutRequest(Guid? WorkoutId, List<SetRequest> Sets);
}
