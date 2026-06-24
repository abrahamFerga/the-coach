using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using TheCoach.Application.AiGeneration.Domain;
using TheCoach.Application.AiGeneration.Persistence;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Application.AiGeneration.Services;

public class AiGenerationService
{
    private readonly AiGenerationDbContext _db;
    private readonly IAiGenerationGateway _gateway;
    private readonly ITenantContext _tenant;

    public AiGenerationService(AiGenerationDbContext db, IAiGenerationGateway gateway, ITenantContext tenant)
    {
        _db = db;
        _gateway = gateway;
        _tenant = tenant;
    }

    public async Task<AiDraft> GenerateWorkoutProgramAsync(
        Guid coachId,
        WorkoutGenerationRequest request,
        CancellationToken ct = default)
    {
        var content = await _gateway.GenerateWorkoutProgramAsync(request, ct);
        return await SaveDraftAsync(coachId, DraftType.WorkoutProgram,
            JsonSerializer.Serialize(request), JsonSerializer.Serialize(content), ct);
    }

    public async Task<AiDraft> GenerateMealPlanAsync(
        Guid coachId,
        MealPlanGenerationRequest request,
        CancellationToken ct = default)
    {
        var content = await _gateway.GenerateMealPlanAsync(request, ct);
        return await SaveDraftAsync(coachId, DraftType.MealPlan,
            JsonSerializer.Serialize(request), JsonSerializer.Serialize(content), ct);
    }

    public async Task<List<AiDraft>> ListDraftsAsync(Guid coachId, CancellationToken ct = default) =>
        await _db.AiDrafts.AsNoTracking()
            .Where(d => d.CoachId == coachId)
            .OrderByDescending(d => d.GeneratedAt)
            .ToListAsync(ct);

    public async Task<AiDraft?> GetDraftAsync(Guid draftId, CancellationToken ct = default) =>
        await _db.AiDrafts.AsNoTracking()
            .FirstOrDefaultAsync(d => d.Id == draftId, ct);

    public async Task<AiDraft> AcceptDraftAsync(Guid draftId, Guid coachId, CancellationToken ct = default)
    {
        var draft = await _db.AiDrafts
            .FirstOrDefaultAsync(d => d.Id == draftId && d.CoachId == coachId, ct)
            ?? throw new InvalidOperationException($"Draft {draftId} not found for coach {coachId}.");

        if (draft.Status == DraftStatus.Accepted)
            throw new InvalidOperationException("Draft already accepted.");

        draft.Accept();
        await _db.SaveChangesAsync(ct);
        return draft;
    }

    public async Task<AiDraft> RejectDraftAsync(Guid draftId, Guid coachId, CancellationToken ct = default)
    {
        var draft = await _db.AiDrafts
            .FirstOrDefaultAsync(d => d.Id == draftId && d.CoachId == coachId, ct)
            ?? throw new InvalidOperationException($"Draft {draftId} not found for coach {coachId}.");

        draft.Reject();
        await _db.SaveChangesAsync(ct);
        return draft;
    }

    private async Task<AiDraft> SaveDraftAsync(
        Guid coachId,
        DraftType type,
        string prompt,
        string contentJson,
        CancellationToken ct)
    {
        var draft = new AiDraft
        {
            CoachId = coachId,
            DraftType = type,
            Prompt = prompt,
            ContentJson = contentJson,
            TenantId = _tenant.TenantId
        };
        _db.AiDrafts.Add(draft);
        await _db.SaveChangesAsync(ct);
        return draft;
    }
}
