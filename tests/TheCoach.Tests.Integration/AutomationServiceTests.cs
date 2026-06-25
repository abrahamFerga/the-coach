using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using TheCoach.Application.Automations.Domain;
using TheCoach.Application.Automations.Persistence;
using TheCoach.Application.Automations.Services;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Tests.Integration;

public class AutomationServiceTests
{
    private static (AutomationsDbContext db, AutomationService svc) Build(Guid tenantId, string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AutomationsDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        var tenant = new StubTenantContext(tenantId);
        var db = new AutomationsDbContext(options, tenant);
        var dispatcher = new LoggingActionDispatcher(NullLogger<LoggingActionDispatcher>.Instance);
        return (db, new AutomationService(db, tenant, dispatcher));
    }

    private static AutomationStep[] TwoSteps() =>
    [
        new AutomationStep(1, AutomationActionType.SendMessage, 0, "{\"message\":\"Welcome!\"}"),
        new AutomationStep(2, AutomationActionType.AssignProgram, 7, "{\"programId\":\"abc\"}"),
    ];

    [Fact]
    public async Task CreateWorkflow_persists_with_steps()
    {
        var tenantId = Guid.NewGuid();
        var (_, svc) = Build(tenantId);

        var wf = await svc.CreateWorkflowAsync("Onboarding", AutomationTrigger.ClientEnrolled, TwoSteps());

        wf.Id.Should().NotBeEmpty();
        wf.TenantId.Should().Be(tenantId);
        wf.IsEnabled.Should().BeTrue("new workflows start enabled by default");
        wf.GetSteps().Should().HaveCount(2);
    }

    [Fact]
    public async Task Enable_and_Disable_toggle_IsEnabled()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (_, svc) = Build(tenantId, dbName);
        var wf = await svc.CreateWorkflowAsync("Toggle", AutomationTrigger.CheckInSubmitted, TwoSteps());

        var (_, svc2) = Build(tenantId, dbName);
        await svc2.EnableAsync(wf.Id);
        var (db3, svc3) = Build(tenantId, dbName);
        (await db3.AutomationWorkflows.FindAsync(wf.Id))!.IsEnabled.Should().BeTrue();

        await svc3.DisableAsync(wf.Id);
        var (db4, _) = Build(tenantId, dbName);
        (await db4.AutomationWorkflows.FindAsync(wf.Id))!.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public async Task Trigger_creates_run_and_outbox_items()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var (_, svc) = Build(tenantId, dbName);
        var wf = await svc.CreateWorkflowAsync("Drip", AutomationTrigger.ClientEnrolled, TwoSteps());

        var (_, svc2) = Build(tenantId, dbName);
        await svc2.EnableAsync(wf.Id);

        var (db3, svc3) = Build(tenantId, dbName);
        var run = await svc3.TriggerAsync(AutomationTrigger.ClientEnrolled, clientId);

        run.Should().NotBeNull();
        run!.WorkflowId.Should().Be(wf.Id);
        run.ClientId.Should().Be(clientId);
        run.StepsTotal.Should().Be(2);

        var outbox = await db3.AutomationOutboxItems
            .Where(i => i.RunId == run.Id)
            .ToListAsync();
        outbox.Should().HaveCount(2);
        outbox.Should().Contain(i => i.ActionType == AutomationActionType.SendMessage);
        outbox.Should().Contain(i => i.ActionType == AutomationActionType.AssignProgram);
    }

    [Fact]
    public async Task Trigger_returns_null_when_no_enabled_workflow_matches()
    {
        var tenantId = Guid.NewGuid();
        var (_, svc) = Build(tenantId);

        var run = await svc.TriggerAsync(AutomationTrigger.ProgramCompleted, Guid.NewGuid());

        run.Should().BeNull();
    }

    [Fact]
    public async Task ProcessOutbox_dispatches_due_items_and_updates_run()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var (_, svc) = Build(tenantId, dbName);
        var wf = await svc.CreateWorkflowAsync("Immediate", AutomationTrigger.ClientEnrolled, [
            new AutomationStep(1, AutomationActionType.SendMessage, 0, "{}")
        ]);
        var (_, svc2) = Build(tenantId, dbName);
        await svc2.EnableAsync(wf.Id);

        var (_, svc3) = Build(tenantId, dbName);
        var run = await svc3.TriggerAsync(AutomationTrigger.ClientEnrolled, clientId);

        var (db4, svc4) = Build(tenantId, dbName);
        var count = await svc4.ProcessOutboxAsync();

        count.Should().Be(1);

        var updatedRun = await db4.AutomationRuns.FindAsync(run!.Id);
        updatedRun!.StepsCompleted.Should().Be(1);

        var item = await db4.AutomationOutboxItems.FirstAsync(i => i.RunId == run.Id);
        item.ProcessedAt.Should().NotBeNull();
    }

    [Fact]
    public async Task GetRunLog_returns_runs_newest_first()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();

        var (_, svc) = Build(tenantId, dbName);
        var wf = await svc.CreateWorkflowAsync("Log", AutomationTrigger.CheckInSubmitted, [
            new AutomationStep(1, AutomationActionType.SendMessage, 0, "{}")
        ]);
        var (_, svc2) = Build(tenantId, dbName);
        await svc2.EnableAsync(wf.Id);

        var (_, svc3) = Build(tenantId, dbName);
        await svc3.TriggerAsync(AutomationTrigger.CheckInSubmitted, Guid.NewGuid());

        var (_, svc4) = Build(tenantId, dbName);
        await svc4.TriggerAsync(AutomationTrigger.CheckInSubmitted, Guid.NewGuid());

        var (_, svc5) = Build(tenantId, dbName);
        var log = await svc5.GetRunLogAsync();

        log.Should().HaveCount(2);
        log[0].TriggeredAt.Should().BeOnOrAfter(log[1].TriggeredAt);
    }

    [Fact]
    public async Task Workflows_are_isolated_by_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dbName = "auto-isolation";

        var (_, svcA) = Build(tenantA, dbName);
        await svcA.CreateWorkflowAsync("A's workflow", AutomationTrigger.ClientEnrolled, TwoSteps());

        var (_, svcB) = Build(tenantB, dbName);
        var bWorkflows = await svcB.ListWorkflowsAsync();

        bWorkflows.Should().BeEmpty("tenant B must not see tenant A's workflows");
    }

    private record StubTenantContext(Guid TenantId) : ITenantContext
    {
        public string TenantSlug => "stub";
        public string PlanTier => "pro";
        public bool IsSystemAdmin => false;
    }
}
