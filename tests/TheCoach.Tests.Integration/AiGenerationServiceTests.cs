using System.Text.Json;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheCoach.Application.AiGeneration.Domain;
using TheCoach.Application.AiGeneration.Persistence;
using TheCoach.Application.AiGeneration.Services;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Tests.Integration;

public class AiGenerationServiceTests
{
    private static (AiGenerationDbContext db, AiGenerationService svc) Build(Guid tenantId, string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<AiGenerationDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        var tenant = new StubTenantContext(tenantId);
        var db = new AiGenerationDbContext(options, tenant);
        return (db, new AiGenerationService(db, new StubAiGenerationGateway(), tenant));
    }

    [Fact]
    public async Task GenerateWorkoutProgram_creates_draft_with_content()
    {
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (_, svc) = Build(tenantId);

        var request = new WorkoutGenerationRequest(
            "Hypertrophy", 8, 4,
            ["Barbell", "Dumbbells"],
            [],
            ["Barbell Bench Press", "Barbell Row", "Barbell Overhead Press",
             "Squat", "Romanian Deadlift", "Pull-Up"]);

        var draft = await svc.GenerateWorkoutProgramAsync(coachId, request);

        draft.TenantId.Should().Be(tenantId);
        draft.DraftType.Should().Be(DraftType.WorkoutProgram);
        draft.Status.Should().Be(DraftStatus.Ready);
        draft.ContentJson.Should().NotBe("{}");

        var content = JsonSerializer.Deserialize<WorkoutProgramDraft>(draft.ContentJson);
        content.Should().NotBeNull();
        content!.Blocks.Should().NotBeEmpty();
        content.Blocks[0].Workouts.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GenerateMealPlan_creates_draft_with_7_days()
    {
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var (_, svc) = Build(tenantId);

        var request = new MealPlanGenerationRequest(2500m, 200m, 280m, 70m, []);
        var draft = await svc.GenerateMealPlanAsync(coachId, request);

        draft.DraftType.Should().Be(DraftType.MealPlan);

        var content = JsonSerializer.Deserialize<MealPlanDraft>(draft.ContentJson);
        content!.Days.Should().HaveCount(7);
        content.ShoppingList.Should().NotBeEmpty();
    }

    [Fact]
    public async Task ListDrafts_returns_coach_drafts_ordered_newest_first()
    {
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var request = new WorkoutGenerationRequest("Strength", 4, 3, [], [], []);

        var (_, svc) = Build(tenantId, dbName);
        await svc.GenerateWorkoutProgramAsync(coachId, request);
        await svc.GenerateWorkoutProgramAsync(coachId, request);

        var (_, svc2) = Build(tenantId, dbName);
        var list = await svc2.ListDraftsAsync(coachId);

        list.Should().HaveCount(2);
        list[0].GeneratedAt.Should().BeOnOrAfter(list[1].GeneratedAt);
    }

    [Fact]
    public async Task AcceptDraft_changes_status_to_accepted()
    {
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var request = new WorkoutGenerationRequest("Fat Loss", 6, 4, [], [], []);

        var (_, svc) = Build(tenantId, dbName);
        var draft = await svc.GenerateWorkoutProgramAsync(coachId, request);

        var (_, svc2) = Build(tenantId, dbName);
        var accepted = await svc2.AcceptDraftAsync(draft.Id, coachId);

        accepted.Status.Should().Be(DraftStatus.Accepted);
    }

    [Fact]
    public async Task AcceptDraft_throws_if_already_accepted()
    {
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var request = new WorkoutGenerationRequest("Endurance", 8, 5, [], [], []);

        var (_, svc) = Build(tenantId, dbName);
        var draft = await svc.GenerateWorkoutProgramAsync(coachId, request);

        var (_, svc2) = Build(tenantId, dbName);
        await svc2.AcceptDraftAsync(draft.Id, coachId);

        var (_, svc3) = Build(tenantId, dbName);
        var act = async () => await svc3.AcceptDraftAsync(draft.Id, coachId);
        await act.Should().ThrowAsync<InvalidOperationException>("cannot accept an already accepted draft");
    }

    [Fact]
    public async Task Drafts_are_isolated_by_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dbName = "ai-isolation";
        var coachId = Guid.NewGuid();
        var request = new WorkoutGenerationRequest("Strength", 4, 3, [], [], []);

        var (_, svcA) = Build(tenantA, dbName);
        await svcA.GenerateWorkoutProgramAsync(coachId, request);

        var (_, svcB) = Build(tenantB, dbName);
        var drafts = await svcB.ListDraftsAsync(coachId);

        drafts.Should().BeEmpty("drafts from tenantA must not be visible to tenantB");
    }

    private record StubTenantContext(Guid TenantId) : ITenantContext
    {
        public string TenantSlug => "stub";
        public string PlanTier => "pro";
        public bool IsSystemAdmin => false;
    }
}
