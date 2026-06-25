using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Coaching.Domain;
using TheCoach.Application.Coaching.Persistence;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Application.Coaching.Services;

public class ExerciseService
{
    private readonly CoachingDbContext _db;
    private readonly ITenantContext _tenant;

    public ExerciseService(CoachingDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<Exercise>> SearchAsync(string? query, CancellationToken ct = default)
    {
        var q = _db.Exercises.AsNoTracking()
            .Where(e => e.TenantId == Guid.Empty || e.TenantId == _tenant.TenantId);

        if (!string.IsNullOrWhiteSpace(query))
            q = q.Where(e => EF.Functions.ILike(e.Name, $"%{query}%"));

        return await q.OrderBy(e => e.Name).Take(50).ToListAsync(ct);
    }

    public async Task<Exercise> CreateTenantExerciseAsync(string name, string muscleGroup, string? demoVideoUrl, CancellationToken ct = default)
    {
        var exercise = new Exercise
        {
            Name = name,
            MuscleGroup = muscleGroup,
            DemoVideoUrl = demoVideoUrl,
            TenantId = _tenant.TenantId
        };
        _db.Exercises.Add(exercise);
        await _db.SaveChangesAsync(ct);
        return exercise;
    }
}
