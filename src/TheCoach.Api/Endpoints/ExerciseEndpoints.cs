using Microsoft.AspNetCore.Mvc;
using TheCoach.Application.Coaching.Services;
using TheCoach.Application.Foundations.Auth;

namespace TheCoach.Api.Endpoints;

public static class ExerciseEndpoints
{
    public static IEndpointRouteBuilder MapExerciseEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/exercises")
            .RequireAuthorization()
            .WithTags("Exercises");

        group.MapGet("/", async (
            [FromQuery] string? q,
            ExerciseService svc,
            CancellationToken ct) =>
        {
            var exercises = await svc.SearchAsync(q, ct);
            return Results.Ok(exercises.Select(e => new
            {
                e.Id,
                e.Name,
                e.MuscleGroup,
                e.DemoVideoUrl,
                e.Tags,
                e.IsGlobal
            }));
        });

        group.MapPost("/", async (
            CreateExerciseRequest req,
            ExerciseService svc,
            CancellationToken ct) =>
        {
            var exercise = await svc.CreateTenantExerciseAsync(req.Name, req.MuscleGroup, req.DemoVideoUrl, ct);
            return Results.Created($"/api/v1/exercises/{exercise.Id}", new { exercise.Id, exercise.Name, exercise.MuscleGroup });
        }).RequireAuthorization(Policies.ProgramsCreate);

        return app;
    }

    private record CreateExerciseRequest(string Name, string MuscleGroup, string? DemoVideoUrl);
}
