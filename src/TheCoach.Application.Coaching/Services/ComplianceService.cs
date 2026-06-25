using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Coaching.Domain;
using TheCoach.Application.Coaching.Persistence;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Application.Coaching.Services;

public record ClientAdherenceRow(
    Guid ClientId,
    DateTimeOffset? LastLoggedAt,
    AdherenceStatus Status,
    int OpenAlerts);

public enum AdherenceStatus { Green, Amber, Red }

public class ComplianceService
{
    private readonly CoachingDbContext _db;
    private readonly ITenantContext _tenant;

    public ComplianceService(CoachingDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<ClientAdherenceRow>> GetRosterAsync(CancellationToken ct = default)
    {
        var now = DateTimeOffset.UtcNow;

        var activeClientIds = await _db.ProgramAssignments
            .AsNoTracking()
            .Where(a => a.Status == AssignmentStatus.Active)
            .Select(a => a.ClientId)
            .Distinct()
            .ToListAsync(ct);

        var lastLogs = await _db.WorkoutLogs
            .AsNoTracking()
            .Where(l => activeClientIds.Contains(l.ClientId))
            .GroupBy(l => l.ClientId)
            .Select(g => new { ClientId = g.Key, LastAt = g.Max(l => l.LoggedAt) })
            .ToDictionaryAsync(x => x.ClientId, x => x.LastAt, ct);

        var openAlertCounts = await _db.ComplianceAlerts
            .AsNoTracking()
            .Where(a => a.AcknowledgedAt == null && activeClientIds.Contains(a.ClientId))
            .GroupBy(a => a.ClientId)
            .Select(g => new { ClientId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.ClientId, x => x.Count, ct);

        return activeClientIds
            .Select(clientId =>
            {
                var lastAt = lastLogs.TryGetValue(clientId, out var at) ? (DateTimeOffset?)at : null;
                var daysSince = lastAt.HasValue ? (now - lastAt.Value).TotalDays : double.MaxValue;
                var status = daysSince switch
                {
                    <= 1 => AdherenceStatus.Green,
                    <= 3 => AdherenceStatus.Amber,
                    _ => AdherenceStatus.Red
                };
                return new ClientAdherenceRow(clientId, lastAt, status, openAlertCounts.GetValueOrDefault(clientId, 0));
            })
            .OrderByDescending(r => r.Status)
            .ThenBy(r => r.LastLoggedAt ?? DateTimeOffset.MinValue)
            .ToList();
    }

    public async Task<List<ComplianceAlert>> GetOpenAlertsForCoachAsync(Guid coachId, CancellationToken ct = default) =>
        await _db.ComplianceAlerts.AsNoTracking()
            .Where(a => a.CoachId == coachId && a.AcknowledgedAt == null)
            .OrderByDescending(a => a.TriggeredAt)
            .ToListAsync(ct);

    public async Task<ComplianceAlert?> AcknowledgeAsync(Guid alertId, Guid coachId, CancellationToken ct = default)
    {
        var alert = await _db.ComplianceAlerts.FirstOrDefaultAsync(a => a.Id == alertId, ct);
        if (alert is null) return null;
        alert.Acknowledge(coachId);
        await _db.SaveChangesAsync(ct);
        return alert;
    }

    public async Task<int> ScanAndRaiseAlertsAsync(CancellationToken ct = default)
    {
        var threshold = DateTimeOffset.UtcNow.AddDays(-3);
        var raised = 0;

        var activeAssignments = await _db.ProgramAssignments
            .AsNoTracking()
            .Where(a => a.Status == AssignmentStatus.Active)
            .ToListAsync(ct);

        foreach (var assignment in activeAssignments)
        {
            var lastLog = await _db.WorkoutLogs.AsNoTracking()
                .Where(l => l.ClientId == assignment.ClientId)
                .OrderByDescending(l => l.LoggedAt)
                .Select(l => l.LoggedAt)
                .FirstOrDefaultAsync(ct);

            var isSilent = lastLog == default || lastLog < threshold;
            if (!isSilent) continue;

            var existingOpen = await _db.ComplianceAlerts.AnyAsync(
                a => a.ClientId == assignment.ClientId && a.AcknowledgedAt == null, ct);
            if (existingOpen) continue;

            _db.ComplianceAlerts.Add(new ComplianceAlert
            {
                CoachId = Guid.Empty,
                ClientId = assignment.ClientId,
                TenantId = assignment.TenantId
            });
            raised++;
        }

        if (raised > 0)
            await _db.SaveChangesAsync(ct);

        return raised;
    }
}
