using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Automations.Domain;
using TheCoach.Application.Automations.Persistence;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Application.Automations.Services;

public class AutomationService
{
    private readonly AutomationsDbContext _db;
    private readonly ITenantContext _tenant;
    private readonly IAutomationActionDispatcher _dispatcher;

    public AutomationService(
        AutomationsDbContext db,
        ITenantContext tenant,
        IAutomationActionDispatcher dispatcher)
    {
        _db = db;
        _tenant = tenant;
        _dispatcher = dispatcher;
    }

    public async Task<AutomationWorkflow> CreateWorkflowAsync(
        string name,
        AutomationTrigger trigger,
        IEnumerable<AutomationStep> steps,
        CancellationToken ct = default)
    {
        var workflow = new AutomationWorkflow
        {
            Name = name,
            TriggerEvent = trigger,
            TenantId = _tenant.TenantId
        };
        workflow.SetSteps(steps);
        _db.AutomationWorkflows.Add(workflow);
        await _db.SaveChangesAsync(ct);
        return workflow;
    }

    public async Task<List<AutomationWorkflow>> ListWorkflowsAsync(CancellationToken ct = default) =>
        await _db.AutomationWorkflows.AsNoTracking()
            .OrderBy(w => w.Name)
            .ToListAsync(ct);

    public async Task EnableAsync(Guid workflowId, CancellationToken ct = default)
    {
        var wf = await GetWorkflowAsync(workflowId, ct);
        wf.Enable();
        await _db.SaveChangesAsync(ct);
    }

    public async Task DisableAsync(Guid workflowId, CancellationToken ct = default)
    {
        var wf = await GetWorkflowAsync(workflowId, ct);
        wf.Disable();
        await _db.SaveChangesAsync(ct);
    }

    public async Task<AutomationRun?> TriggerAsync(
        AutomationTrigger trigger,
        Guid clientId,
        CancellationToken ct = default)
    {
        var workflow = await _db.AutomationWorkflows
            .Where(w => w.TriggerEvent == trigger && w.IsEnabled)
            .FirstOrDefaultAsync(ct);

        if (workflow is null) return null;

        var steps = workflow.GetSteps();
        var run = new AutomationRun
        {
            WorkflowId = workflow.Id,
            ClientId = clientId,
            StepsTotal = steps.Count,
            TenantId = _tenant.TenantId
        };
        _db.AutomationRuns.Add(run);
        await _db.SaveChangesAsync(ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var step in steps)
        {
            _db.AutomationOutboxItems.Add(new AutomationOutboxItem
            {
                RunId = run.Id,
                StepOrder = step.Order,
                ActionType = step.ActionType,
                Payload = step.Payload,
                ScheduledFor = now.AddDays(step.DelayDays),
                TenantId = _tenant.TenantId
            });
        }
        await _db.SaveChangesAsync(ct);
        return run;
    }

    public async Task<int> ProcessOutboxAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var due = await _db.AutomationOutboxItems
            .Where(i => i.ProcessedAt == null && i.ScheduledFor <= now && i.RetryCount < 5)
            .OrderBy(i => i.ScheduledFor)
            .Take(50)
            .ToListAsync(ct);

        var processed = 0;
        foreach (var item in due)
        {
            try
            {
                await _dispatcher.DispatchAsync(item, ct);
                item.MarkProcessed();

                var run = await _db.AutomationRuns.FindAsync([item.RunId], ct);
                run?.RecordStepCompleted();
                processed++;
            }
            catch (Exception ex)
            {
                item.RecordFailure(ex.Message);
            }
        }

        if (processed > 0 || due.Any(i => i.LastError != null))
            await _db.SaveChangesAsync(ct);

        return processed;
    }

    public async Task<List<AutomationRun>> GetRunLogAsync(
        int limit = 50,
        CancellationToken ct = default) =>
        await _db.AutomationRuns.AsNoTracking()
            .OrderByDescending(r => r.TriggeredAt)
            .Take(limit)
            .ToListAsync(ct);

    private async Task<AutomationWorkflow> GetWorkflowAsync(Guid id, CancellationToken ct) =>
        await _db.AutomationWorkflows.FindAsync([id], ct)
            ?? throw new InvalidOperationException($"Workflow {id} not found.");
}
