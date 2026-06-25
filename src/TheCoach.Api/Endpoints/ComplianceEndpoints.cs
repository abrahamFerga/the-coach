using System.Security.Claims;
using TheCoach.Application.Coaching.Services;
using TheCoach.Application.Foundations.Auth;

namespace TheCoach.Api.Endpoints;

public static class ComplianceEndpoints
{
    public static IEndpointRouteBuilder MapComplianceEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/compliance")
            .RequireAuthorization()
            .WithTags("Compliance");

        group.MapGet("/roster", async (
            ComplianceService svc,
            CancellationToken ct) =>
        {
            var roster = await svc.GetRosterAsync(ct);
            return Results.Ok(roster.Select(r => new
            {
                r.ClientId,
                r.LastLoggedAt,
                Status = r.Status.ToString(),
                r.OpenAlerts
            }));
        }).RequireAuthorization(Policies.ComplianceViewOwn);

        group.MapGet("/alerts", async (
            ComplianceService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var coachId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var alerts = await svc.GetOpenAlertsForCoachAsync(coachId, ct);
            return Results.Ok(alerts.Select(a => new
            {
                a.Id,
                a.ClientId,
                a.CoachId,
                a.TriggeredAt,
                a.IsAcknowledged
            }));
        }).RequireAuthorization(Policies.ComplianceViewOwn);

        group.MapPost("/alerts/{alertId:guid}/acknowledge", async (
            Guid alertId,
            ComplianceService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var coachId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var alert = await svc.AcknowledgeAsync(alertId, coachId, ct);
            return alert is null
                ? Results.NotFound()
                : Results.Ok(new { alert.Id, alert.AcknowledgedAt, alert.AcknowledgedByCoachId });
        }).RequireAuthorization(Policies.ComplianceViewOwn);

        return app;
    }
}
