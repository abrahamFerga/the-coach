using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Coaching.Domain;
using TheCoach.Application.Coaching.Persistence;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Application.Coaching.Services;

public class WorkoutLogService
{
    private readonly CoachingDbContext _db;
    private readonly ITenantContext _tenant;

    public WorkoutLogService(CoachingDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<WorkoutLog> LogAsync(Guid clientId, Guid? workoutId, IEnumerable<SetEntry> sets, CancellationToken ct = default)
    {
        var log = new WorkoutLog
        {
            ClientId = clientId,
            WorkoutId = workoutId,
            LoggedAt = DateTimeOffset.UtcNow,
            TenantId = _tenant.TenantId
        };
        log.SetSets(sets);
        _db.WorkoutLogs.Add(log);
        await _db.SaveChangesAsync(ct);
        return log;
    }

    public async Task<List<WorkoutLog>> GetClientHistoryAsync(Guid clientId, int pageSize = 20, int page = 0, CancellationToken ct = default) =>
        await _db.WorkoutLogs.AsNoTracking()
            .Where(wl => wl.ClientId == clientId)
            .OrderByDescending(wl => wl.LoggedAt)
            .Skip(page * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<SetEntry>> GetPreviousSessionSetsAsync(Guid clientId, Guid workoutId, CancellationToken ct = default)
    {
        var lastLog = await _db.WorkoutLogs.AsNoTracking()
            .Where(wl => wl.ClientId == clientId && wl.WorkoutId == workoutId)
            .OrderByDescending(wl => wl.LoggedAt)
            .FirstOrDefaultAsync(ct);

        return lastLog?.GetSets() ?? [];
    }
}
