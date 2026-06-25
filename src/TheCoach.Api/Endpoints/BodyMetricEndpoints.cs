using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TheCoach.Application.Foundations.Auth;
using TheCoach.Application.HealthTracking.Services;

namespace TheCoach.Api.Endpoints;

public static class BodyMetricEndpoints
{
    public static IEndpointRouteBuilder MapBodyMetricEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/metrics")
            .RequireAuthorization()
            .WithTags("BodyMetrics");

        group.MapPost("/", async (
            LogMetricRequest req,
            BodyMetricService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var clientId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var metric = await svc.LogAsync(clientId, req.Date, req.WeightKg, req.BodyFatPercent,
                req.Measurements, req.PhotoBlobUrl, ct);
            return Results.Ok(MapMetric(metric));
        }).RequireAuthorization(Policies.HealthTrackingViewOwn);

        group.MapGet("/client/{clientId:guid}/trend", async (
            Guid clientId,
            [FromQuery] string window,
            BodyMetricService svc,
            CancellationToken ct) =>
        {
            var w = ParseWindow(window);
            var metrics = await svc.GetTrendAsync(clientId, w, ct);
            return Results.Ok(metrics.Select(MapMetric));
        });

        group.MapGet("/client/{clientId:guid}/personal-bests", async (
            Guid clientId,
            BodyMetricService svc,
            CancellationToken ct) =>
        {
            var pbs = await svc.GetPersonalBestsAsync(clientId, ct);
            return Results.Ok(pbs.Select(pb => new { pb.Metric, pb.Value, pb.AchievedOn }));
        });

        return app;
    }

    private static MetricWindow ParseWindow(string window) => window switch
    {
        "4w" => new MetricWindow(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-28)), DateOnly.FromDateTime(DateTime.UtcNow)),
        "12w" => new MetricWindow(DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-84)), DateOnly.FromDateTime(DateTime.UtcNow)),
        _ => new MetricWindow(DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-10)), DateOnly.FromDateTime(DateTime.UtcNow))
    };

    private static object MapMetric(Application.HealthTracking.Domain.BodyMetric m) => new
    {
        m.Id,
        m.ClientId,
        m.RecordedOn,
        m.WeightKg,
        m.BodyFatPercent,
        Measurements = m.GetMeasurements(),
        m.PhotoBlobUrl
    };

    private record LogMetricRequest(
        DateOnly Date,
        decimal? WeightKg,
        decimal? BodyFatPercent,
        Dictionary<string, decimal>? Measurements,
        string? PhotoBlobUrl);
}
