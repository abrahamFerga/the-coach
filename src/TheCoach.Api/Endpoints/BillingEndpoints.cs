using System.Security.Claims;
using TheCoach.Application.Billing.Services;
using TheCoach.Application.Foundations.Auth;

namespace TheCoach.Api.Endpoints;

public static class BillingEndpoints
{
    public static IEndpointRouteBuilder MapBillingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/billing")
            .RequireAuthorization()
            .WithTags("Billing");

        group.MapGet("/subscription", async (
            BillingService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var tenantId = GetTenantId(user);
            var sub = await svc.GetOrCreateSubscriptionAsync(tenantId, ct);
            return Results.Ok(new
            {
                sub.TenantId,
                sub.PlanTier,
                Status = sub.Status.ToString(),
                sub.TrialEndsAt,
                sub.CurrentPeriodEnd
            });
        }).RequireAuthorization(Policies.BillingManage);

        group.MapPost("/portal-session", async (
            PortalSessionRequest req,
            BillingService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var tenantId = GetTenantId(user);
            var url = await svc.GetBillingPortalUrlAsync(tenantId, req.ReturnUrl, ct);
            return Results.Ok(new { Url = url });
        }).RequireAuthorization(Policies.BillingManage);

        // Stripe webhook — no auth (verified by signature in prod; omitted here for scope)
        group.MapPost("/webhook", async (
            WebhookPayload payload,
            BillingService svc,
            CancellationToken ct) =>
        {
            var processed = await svc.HandleWebhookAsync(
                payload.StripeEventId,
                payload.EventType,
                payload.TenantId,
                payload.StripeSubscriptionId,
                payload.PeriodEnd,
                ct);
            return Results.Ok(new { Processed = processed });
        }).AllowAnonymous();

        return app;
    }

    private static Guid GetTenantId(ClaimsPrincipal user) =>
        Guid.Parse(user.FindFirstValue("tenant_id") ?? user.FindFirstValue(ClaimTypes.NameIdentifier)!);

    private record PortalSessionRequest(string ReturnUrl);

    private record WebhookPayload(
        string StripeEventId,
        string EventType,
        Guid? TenantId,
        string? StripeSubscriptionId,
        DateTimeOffset? PeriodEnd);
}
