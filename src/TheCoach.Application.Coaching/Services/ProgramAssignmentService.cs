using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Coaching.Domain;
using TheCoach.Application.Coaching.Persistence;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Application.Coaching.Services;

public class ProgramAssignmentService
{
    private readonly CoachingDbContext _db;
    private readonly ITenantContext _tenant;

    public ProgramAssignmentService(CoachingDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<ProgramAssignment> AssignAsync(Guid programId, Guid clientId, DateOnly startDate, CancellationToken ct = default)
    {
        var existing = await _db.ProgramAssignments
            .FirstOrDefaultAsync(a => a.ProgramId == programId && a.ClientId == clientId && a.Status == AssignmentStatus.Active, ct);

        if (existing is not null)
            return existing;

        var assignment = new ProgramAssignment
        {
            ProgramId = programId,
            ClientId = clientId,
            StartDate = startDate,
            TenantId = _tenant.TenantId
        };
        _db.ProgramAssignments.Add(assignment);
        await _db.SaveChangesAsync(ct);
        return assignment;
    }

    public async Task<List<ProgramAssignment>> GetClientAssignmentsAsync(Guid clientId, CancellationToken ct = default) =>
        await _db.ProgramAssignments.AsNoTracking()
            .Where(a => a.ClientId == clientId && a.Status == AssignmentStatus.Active)
            .ToListAsync(ct);
}
