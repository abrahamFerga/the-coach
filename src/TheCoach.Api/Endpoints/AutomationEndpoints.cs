using TheCoach.Application.Automations.Domain;
using TheCoach.Application.Automations.Services;
using TheCoach.Application.Foundations.Auth;

namespace TheCoach.Api.Endpoints;

public static class AutomationEndpoints
{
    public static IEndpointRouteBuilder MapAutomationEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/automations")
            .RequireAuthorization()
            .WithTags("Automations");

        group.MapPost("/", async (
            CreateWorkflowRequest req,
            AutomationService svc,
            CancellationToken ct) =>
        {
            var steps = req.Steps.Select((s, i) =>
                new AutomationStep(i + 1, s.ActionType, s.DelayDays, s.Payload));
            var wf = await svc.CreateWorkflowAsync(req.Name, req.TriggerEvent, steps, ct);
            return Results.Ok(MapWorkflow(wf));
        }).RequireAuthorization(Policies.AutomationsManage);

        group.MapGet("/", async (AutomationService svc, CancellationToken ct) =>
        {
            var workflows = await svc.ListWorkflowsAsync(ct);
            return Results.Ok(workflows.Select(MapWorkflow));
        }).RequireAuthorization(Policies.AutomationsManage);

        group.MapPost("/{workflowId:guid}/enable", async (
            Guid workflowId, AutomationService svc, CancellationToken ct) =>
        {
            await svc.EnableAsync(workflowId, ct);
            return Results.Ok();
        }).RequireAuthorization(Policies.AutomationsManage);

        group.MapPost("/{workflowId:guid}/disable", async (
            Guid workflowId, AutomationService svc, CancellationToken ct) =>
        {
            await svc.DisableAsync(workflowId, ct);
            return Results.Ok();
        }).RequireAuthorization(Policies.AutomationsManage);

        group.MapGet("/runs", async (AutomationService svc, CancellationToken ct) =>
        {
            var runs = await svc.GetRunLogAsync(ct: ct);
            return Results.Ok(runs.Select(r => new
            {
                r.Id, r.WorkflowId, r.ClientId,
                r.TriggeredAt, Status = r.Status.ToString(),
                r.StepsCompleted, r.StepsTotal
            }));
        }).RequireAuthorization(Policies.AutomationsManage);

        return app;
    }

    private static object MapWorkflow(AutomationWorkflow w) => new
    {
        w.Id,
        w.Name,
        w.TriggerEvent,
        w.IsEnabled,
        Steps = w.GetSteps()
    };

    private record CreateWorkflowRequest(
        string Name,
        AutomationTrigger TriggerEvent,
        StepDto[] Steps);

    private record StepDto(
        AutomationActionType ActionType,
        int DelayDays,
        string Payload);
}
