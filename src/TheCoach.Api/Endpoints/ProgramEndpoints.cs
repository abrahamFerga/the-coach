using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;
using TheCoach.Application.Coaching.Services;
using TheCoach.Application.Foundations.Auth;

namespace TheCoach.Api.Endpoints;

public static class ProgramEndpoints
{
    public static IEndpointRouteBuilder MapProgramEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/programs")
            .RequireAuthorization()
            .WithTags("Programs");

        group.MapGet("/", async (ProgramService svc, CancellationToken ct) =>
        {
            var programs = await svc.ListAsync(ct);
            return Results.Ok(programs.Select(MapProgram));
        });

        group.MapGet("/{id:guid}", async (Guid id, ProgramService svc, CancellationToken ct) =>
        {
            var program = await svc.GetAsync(id, ct);
            return program is null ? Results.NotFound() : Results.Ok(MapProgram(program));
        });

        group.MapPost("/", async (
            CreateProgramRequest req,
            ProgramService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var coachId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var program = await svc.CreateAsync(req.Name, req.IsTemplate, coachId, ct);
            return Results.Created($"/api/v1/programs/{program.Id}", MapProgram(program));
        }).RequireAuthorization(Policies.ProgramsCreate);

        group.MapPost("/{programId:guid}/blocks", async (
            Guid programId,
            AddBlockRequest req,
            ProgramService svc,
            CancellationToken ct) =>
        {
            var block = await svc.AddBlockAsync(programId, req.Name, req.WeekNumber, ct);
            return Results.Created($"/api/v1/programs/{programId}/blocks/{block.Id}", new { block.Id, block.Name, block.WeekNumber });
        }).RequireAuthorization(Policies.ProgramsCreate);

        group.MapPost("/blocks/{blockId:guid}/workouts", async (
            Guid blockId,
            AddWorkoutRequest req,
            ProgramService svc,
            CancellationToken ct) =>
        {
            var workout = await svc.AddWorkoutAsync(blockId, req.Name, req.DayOfWeek, ct);
            return Results.Created($"/api/v1/programs/workouts/{workout.Id}", new { workout.Id, workout.Name, workout.DayOfWeek });
        }).RequireAuthorization(Policies.ProgramsCreate);

        group.MapPost("/workouts/{workoutId:guid}/exercises", async (
            Guid workoutId,
            AddExerciseRequest req,
            ProgramService svc,
            CancellationToken ct) =>
        {
            var we = await svc.AddExerciseToWorkoutAsync(workoutId, req.ExerciseId, req.SetCount, req.RepTarget, req.WeightTarget, req.RpeTarget, req.Order, ct);
            return Results.Created($"/api/v1/programs/workout-exercises/{we.Id}", new { we.Id, we.ExerciseId, we.SetCount, we.RepTarget });
        }).RequireAuthorization(Policies.ProgramsCreate);

        group.MapPost("/{programId:guid}/assignments", async (
            Guid programId,
            AssignProgramRequest req,
            ProgramAssignmentService svc,
            CancellationToken ct) =>
        {
            var assignment = await svc.AssignAsync(programId, req.ClientId, req.StartDate, ct);
            return Results.Created($"/api/v1/programs/{programId}/assignments/{assignment.Id}", new { assignment.Id, assignment.ClientId, assignment.StartDate, assignment.Status });
        }).RequireAuthorization(Policies.ProgramsAssign);

        group.MapGet("/assignments/client/{clientId:guid}", async (
            Guid clientId,
            ProgramAssignmentService svc,
            CancellationToken ct) =>
        {
            var assignments = await svc.GetClientAssignmentsAsync(clientId, ct);
            return Results.Ok(assignments.Select(a => new { a.Id, a.ProgramId, a.StartDate, a.Status }));
        });

        return app;
    }

    private static object MapProgram(Application.Coaching.Domain.Program p) => new
    {
        p.Id,
        p.Name,
        p.IsTemplate,
        p.CreatedByCoachId,
        Blocks = p.Blocks.OrderBy(b => b.WeekNumber).Select(b => new
        {
            b.Id,
            b.Name,
            b.WeekNumber,
            Workouts = b.Workouts.OrderBy(w => w.DayOfWeek).Select(w => new
            {
                w.Id,
                w.Name,
                w.DayOfWeek,
                Exercises = w.Exercises.OrderBy(e => e.Order).Select(e => new
                {
                    e.Id,
                    e.ExerciseId,
                    e.SetCount,
                    e.RepTarget,
                    e.WeightTarget,
                    e.RpeTarget,
                    e.Order
                })
            })
        })
    };

    private record CreateProgramRequest(string Name, bool IsTemplate = false);
    private record AddBlockRequest(string Name, int WeekNumber);
    private record AddWorkoutRequest(string Name, DayOfWeek DayOfWeek);
    private record AddExerciseRequest(Guid ExerciseId, int SetCount, int RepTarget, decimal? WeightTarget, decimal? RpeTarget, int Order = 0);
    private record AssignProgramRequest(Guid ClientId, DateOnly StartDate);
}
