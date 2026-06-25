using Microsoft.EntityFrameworkCore;
using TheCoach.Application.CheckIns.Domain;
using TheCoach.Application.CheckIns.Persistence;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Application.CheckIns.Services;

public class CheckInService
{
    private readonly CheckInsDbContext _db;
    private readonly ITenantContext _tenant;

    public CheckInService(CheckInsDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<CheckInTemplate> CreateTemplateAsync(
        string name,
        string? description,
        IEnumerable<CheckInQuestion> questions,
        DayOfWeek? recurrenceDayOfWeek,
        CancellationToken ct = default)
    {
        var template = new CheckInTemplate
        {
            Name = name,
            Description = description,
            RecurrenceDayOfWeek = recurrenceDayOfWeek,
            TenantId = _tenant.TenantId
        };
        template.SetQuestions(questions);
        _db.CheckInTemplates.Add(template);
        await _db.SaveChangesAsync(ct);
        return template;
    }

    public async Task<List<CheckInTemplate>> ListTemplatesAsync(CancellationToken ct = default) =>
        await _db.CheckInTemplates.AsNoTracking()
            .OrderBy(t => t.Name)
            .ToListAsync(ct);

    public async Task<CheckInAssignment> AssignTemplateAsync(
        Guid templateId,
        Guid clientId,
        Guid coachId,
        DateOnly startsOn,
        CancellationToken ct = default)
    {
        var assignment = new CheckInAssignment
        {
            CheckInTemplateId = templateId,
            ClientId = clientId,
            CoachId = coachId,
            StartsOn = startsOn,
            TenantId = _tenant.TenantId
        };
        _db.CheckInAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);
        return assignment;
    }

    public async Task<int> GenerateDueResponsesAsync(DateOnly date, CancellationToken ct = default)
    {
        var dayOfWeek = date.DayOfWeek;

        var dueAssignments = await _db.CheckInAssignments.AsNoTracking()
            .Where(a => a.IsActive && a.StartsOn <= date)
            .Join(_db.CheckInTemplates.IgnoreQueryFilters(),
                a => a.CheckInTemplateId,
                t => t.Id,
                (a, t) => new { Assignment = a, Template = t })
            .Where(x => x.Template.RecurrenceDayOfWeek == dayOfWeek)
            .ToListAsync(ct);

        var created = 0;
        foreach (var item in dueAssignments)
        {
            var alreadyExists = await _db.CheckInResponses
                .AnyAsync(r => r.AssignmentId == item.Assignment.Id && r.DueDate == date, ct);

            if (alreadyExists) continue;

            var response = new CheckInResponse
            {
                AssignmentId = item.Assignment.Id,
                DueDate = date,
                ExpiresAt = DateTimeOffset.UtcNow.AddHours(48),
                TenantId = item.Assignment.TenantId
            };
            _db.CheckInResponses.Add(response);
            created++;
        }

        if (created > 0)
            await _db.SaveChangesAsync(ct);

        return created;
    }

    public async Task<List<CheckInResponse>> GetDueResponsesForClientAsync(
        Guid clientId,
        CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;
        var assignmentIds = await _db.CheckInAssignments.AsNoTracking()
            .Where(a => a.ClientId == clientId && a.IsActive)
            .Select(a => a.Id)
            .ToListAsync(ct);

        return await _db.CheckInResponses
            .Where(r => assignmentIds.Contains(r.AssignmentId)
                     && r.SubmittedAt == null
                     && r.ExpiresAt > now)
            .OrderBy(r => r.DueDate)
            .ToListAsync(ct);
    }

    public async Task<CheckInResponse> SubmitResponseAsync(
        Guid responseId,
        Guid clientId,
        Dictionary<Guid, string> answers,
        CancellationToken ct = default)
    {
        var assignmentIds = await _db.CheckInAssignments
            .Where(a => a.ClientId == clientId && a.IsActive)
            .Select(a => a.Id)
            .ToListAsync(ct);

        var response = await _db.CheckInResponses
            .FirstOrDefaultAsync(r => r.Id == responseId && assignmentIds.Contains(r.AssignmentId), ct)
            ?? throw new InvalidOperationException($"Check-in response {responseId} not found for client {clientId}.");

        if (response.SubmittedAt != null)
            throw new InvalidOperationException("Check-in response already submitted.");

        if (response.ExpiresAt < DateTimeOffset.UtcNow)
            throw new InvalidOperationException("Check-in response has expired.");

        response.Submit(answers);
        await _db.SaveChangesAsync(ct);
        return response;
    }

    public async Task<List<CheckInResponse>> GetTrendAsync(
        Guid clientId,
        Guid templateId,
        CancellationToken ct = default)
    {
        var assignmentIds = await _db.CheckInAssignments.AsNoTracking()
            .Where(a => a.ClientId == clientId && a.CheckInTemplateId == templateId)
            .Select(a => a.Id)
            .ToListAsync(ct);

        return await _db.CheckInResponses.AsNoTracking()
            .Where(r => assignmentIds.Contains(r.AssignmentId) && r.SubmittedAt != null)
            .OrderBy(r => r.DueDate)
            .ToListAsync(ct);
    }
}
