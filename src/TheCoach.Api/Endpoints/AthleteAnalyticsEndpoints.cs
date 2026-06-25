using TheCoach.Application.AthleteAnalytics.Domain;
using TheCoach.Application.AthleteAnalytics.Services;
using TheCoach.Application.Foundations.Auth;

namespace TheCoach.Api.Endpoints;

public static class AthleteAnalyticsEndpoints
{
    public static IEndpointRouteBuilder MapAthleteAnalyticsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/athlete-analytics")
            .RequireAuthorization()
            .WithTags("AthleteAnalytics");

        group.MapPost("/readiness", async (
            LogReadinessRequest req,
            AthleteAnalyticsService svc,
            CancellationToken ct) =>
        {
            var entry = await svc.LogReadinessAsync(
                req.AthleteId, req.Date, req.ReadinessScore, req.HrvMs, req.Notes, ct);
            return Results.Ok(entry);
        }).RequireAuthorization(Policies.AthleteAnalyticsLog);

        group.MapGet("/readiness/{athleteId:guid}", async (
            Guid athleteId,
            int days,
            AthleteAnalyticsService svc,
            CancellationToken ct) =>
        {
            var trend = await svc.GetReadinessTrendAsync(athleteId, days > 0 ? days : 28, ct);
            return Results.Ok(trend);
        }).RequireAuthorization(Policies.AthleteAnalyticsView);

        group.MapGet("/training-load/{athleteId:guid}", async (
            Guid athleteId,
            AthleteAnalyticsService svc,
            CancellationToken ct) =>
        {
            var load = await svc.GetTrainingLoadAsync(athleteId, ct: ct);
            return Results.Ok(load);
        }).RequireAuthorization(Policies.AthleteAnalyticsView);

        group.MapPost("/periodization", async (
            CreatePlanRequest req,
            AthleteAnalyticsService svc,
            CancellationToken ct) =>
        {
            var plan = await svc.CreatePeriodizationPlanAsync(
                req.CoachId, req.AthleteId, req.Name, req.StartsOn, req.Mesocycles, ct);
            return Results.Ok(new
            {
                plan.Id, plan.Name, plan.AthleteId, plan.CoachId, plan.StartsOn,
                Mesocycles = plan.GetMesocycles()
            });
        }).RequireAuthorization(Policies.AthleteAnalyticsManage);

        group.MapGet("/periodization/{athleteId:guid}", async (
            Guid athleteId,
            AthleteAnalyticsService svc,
            CancellationToken ct) =>
        {
            var plans = await svc.ListPeriodizationPlansAsync(athleteId, ct);
            return Results.Ok(plans.Select(p => new
            {
                p.Id, p.Name, p.AthleteId, p.CoachId, p.StartsOn,
                Mesocycles = p.GetMesocycles()
            }));
        }).RequireAuthorization(Policies.AthleteAnalyticsView);

        group.MapPost("/drills", async (
            LogDrillRequest req,
            AthleteAnalyticsService svc,
            CancellationToken ct) =>
        {
            var result = await svc.LogDrillResultAsync(
                req.AthleteId, req.DrillName, req.MetricName, req.Value, req.LoggedAt, req.Notes, ct);
            return Results.Ok(result);
        }).RequireAuthorization(Policies.AthleteAnalyticsLog);

        group.MapGet("/drills/{athleteId:guid}", async (
            Guid athleteId,
            string drill,
            string metric,
            int days,
            AthleteAnalyticsService svc,
            CancellationToken ct) =>
        {
            var trend = await svc.GetDrillTrendAsync(
                athleteId, drill, metric, days > 0 ? days : 90, ct);
            return Results.Ok(trend);
        }).RequireAuthorization(Policies.AthleteAnalyticsView);

        return app;
    }

    private record LogReadinessRequest(
        Guid AthleteId,
        DateOnly Date,
        int ReadinessScore,
        decimal? HrvMs,
        string? Notes);

    private record CreatePlanRequest(
        Guid CoachId,
        Guid AthleteId,
        string Name,
        DateOnly StartsOn,
        Mesocycle[] Mesocycles);

    private record LogDrillRequest(
        Guid AthleteId,
        string DrillName,
        string MetricName,
        decimal Value,
        DateTimeOffset? LoggedAt,
        string? Notes);
}
