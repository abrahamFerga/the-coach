using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheCoach.Application.CheckIns.Domain;
using TheCoach.Application.CheckIns.Persistence;
using TheCoach.Application.CheckIns.Services;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Tests.Integration;

public class CheckInServiceTests
{
    private static (CheckInsDbContext db, CheckInService svc) Build(Guid tenantId, string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<CheckInsDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        var tenant = new StubTenantContext(tenantId);
        var db = new CheckInsDbContext(options, tenant);
        return (db, new CheckInService(db, tenant));
    }

    [Fact]
    public async Task CreateTemplate_stores_questions_and_is_tenant_scoped()
    {
        var tenantId = Guid.NewGuid();
        var (_, svc) = Build(tenantId);

        var questions = new[]
        {
            new CheckInQuestion(Guid.CreateVersion7(), "Sleep quality", QuestionType.Scale, 1),
            new CheckInQuestion(Guid.CreateVersion7(), "Energy level", QuestionType.Scale, 2)
        };

        var template = await svc.CreateTemplateAsync(
            "Test Check-In", "desc", questions, DayOfWeek.Monday);

        template.TenantId.Should().Be(tenantId);
        template.GetQuestions().Should().HaveCount(2);
        template.RecurrenceDayOfWeek.Should().Be(DayOfWeek.Monday);
    }

    [Fact]
    public async Task AssignTemplate_creates_active_assignment()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (_, svc) = Build(tenantId, dbName);

        var template = await svc.CreateTemplateAsync("Test", null,
            [new CheckInQuestion(Guid.CreateVersion7(), "Q1", QuestionType.Text, 1)],
            DayOfWeek.Wednesday);

        var clientId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var startsOn = DateOnly.FromDateTime(DateTime.UtcNow);

        var assignment = await svc.AssignTemplateAsync(template.Id, clientId, coachId, startsOn);

        assignment.IsActive.Should().BeTrue();
        assignment.ClientId.Should().Be(clientId);
        assignment.CheckInTemplateId.Should().Be(template.Id);
    }

    [Fact]
    public async Task GenerateDueResponses_creates_responses_for_matching_day()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (_, svc) = Build(tenantId, dbName);

        var monday = NextWeekday(DayOfWeek.Monday);

        var template = await svc.CreateTemplateAsync("Weekly", null,
            [new CheckInQuestion(Guid.CreateVersion7(), "Sleep", QuestionType.Scale, 1)],
            DayOfWeek.Monday);

        var clientId = Guid.NewGuid();
        await svc.AssignTemplateAsync(template.Id, clientId, Guid.NewGuid(), monday.AddDays(-1));

        var (_, svc2) = Build(tenantId, dbName);
        var created = await svc2.GenerateDueResponsesAsync(monday);

        created.Should().Be(1);

        var (_, svc3) = Build(tenantId, dbName);
        var due = await svc3.GetDueResponsesForClientAsync(clientId);
        due.Should().ContainSingle(r => r.DueDate == monday);
    }

    [Fact]
    public async Task GenerateDueResponses_is_idempotent()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (_, svc) = Build(tenantId, dbName);

        var monday = NextWeekday(DayOfWeek.Monday);
        var template = await svc.CreateTemplateAsync("Weekly", null,
            [new CheckInQuestion(Guid.CreateVersion7(), "Sleep", QuestionType.Scale, 1)],
            DayOfWeek.Monday);

        await svc.AssignTemplateAsync(template.Id, Guid.NewGuid(), Guid.NewGuid(), monday.AddDays(-1));

        var (_, svc2) = Build(tenantId, dbName);
        await svc2.GenerateDueResponsesAsync(monday);

        var (_, svc3) = Build(tenantId, dbName);
        var secondRun = await svc3.GenerateDueResponsesAsync(monday);

        secondRun.Should().Be(0, "responses already exist for this date");
    }

    [Fact]
    public async Task SubmitResponse_records_answers_and_timestamp()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (_, svc) = Build(tenantId, dbName);

        var monday = NextWeekday(DayOfWeek.Monday);
        var q1Id = Guid.CreateVersion7();
        var template = await svc.CreateTemplateAsync("Weekly", null,
            [new CheckInQuestion(q1Id, "Sleep", QuestionType.Scale, 1)],
            DayOfWeek.Monday);

        var clientId = Guid.NewGuid();
        await svc.AssignTemplateAsync(template.Id, clientId, Guid.NewGuid(), monday.AddDays(-1));

        var (_, svc2) = Build(tenantId, dbName);
        await svc2.GenerateDueResponsesAsync(monday);

        var (_, svc3) = Build(tenantId, dbName);
        var due = await svc3.GetDueResponsesForClientAsync(clientId);
        var responseId = due[0].Id;

        var (_, svc4) = Build(tenantId, dbName);
        var submitted = await svc4.SubmitResponseAsync(responseId, clientId, new() { [q1Id] = "8" });

        submitted.SubmittedAt.Should().NotBeNull();
        submitted.GetAnswers()[q1Id].Should().Be("8");
    }

    [Fact]
    public async Task GetTrendAsync_returns_only_submitted_responses()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (_, svc) = Build(tenantId, dbName);

        var monday = NextWeekday(DayOfWeek.Monday);
        var q1Id = Guid.CreateVersion7();
        var template = await svc.CreateTemplateAsync("Weekly", null,
            [new CheckInQuestion(q1Id, "Sleep", QuestionType.Scale, 1)],
            DayOfWeek.Monday);

        var clientId = Guid.NewGuid();
        await svc.AssignTemplateAsync(template.Id, clientId, Guid.NewGuid(), monday.AddDays(-1));

        var (_, svc2) = Build(tenantId, dbName);
        await svc2.GenerateDueResponsesAsync(monday);

        var (_, svc3) = Build(tenantId, dbName);
        var due = await svc3.GetDueResponsesForClientAsync(clientId);

        var (_, svc4) = Build(tenantId, dbName);
        await svc4.SubmitResponseAsync(due[0].Id, clientId, new() { [q1Id] = "7" });

        var (_, svc5) = Build(tenantId, dbName);
        var trend = await svc5.GetTrendAsync(clientId, template.Id);

        trend.Should().ContainSingle(r => r.SubmittedAt != null);
    }

    [Fact]
    public async Task CheckInResponses_are_isolated_by_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dbName = "ci-isolation";
        var monday = NextWeekday(DayOfWeek.Monday);
        var clientId = Guid.NewGuid();

        var (_, svcA) = Build(tenantA, dbName);
        var template = await svcA.CreateTemplateAsync("Weekly", null,
            [new CheckInQuestion(Guid.CreateVersion7(), "Sleep", QuestionType.Scale, 1)],
            DayOfWeek.Monday);
        await svcA.AssignTemplateAsync(template.Id, clientId, Guid.NewGuid(), monday.AddDays(-1));

        var (_, svcA2) = Build(tenantA, dbName);
        await svcA2.GenerateDueResponsesAsync(monday);

        var (_, svcB) = Build(tenantB, dbName);
        var dueForB = await svcB.GetDueResponsesForClientAsync(clientId);

        dueForB.Should().BeEmpty("responses from tenantA must not be visible to tenantB");
    }

    private static DateOnly NextWeekday(DayOfWeek day)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);
        while (date.DayOfWeek != day)
            date = date.AddDays(1);
        return date;
    }

    private record StubTenantContext(Guid TenantId) : ITenantContext
    {
        public string TenantSlug => "stub";
        public string PlanTier => "pro";
        public bool IsSystemAdmin => false;
    }
}
