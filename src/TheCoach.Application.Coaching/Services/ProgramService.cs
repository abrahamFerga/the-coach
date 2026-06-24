using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Coaching.Domain;
using TheCoach.Application.Coaching.Persistence;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Application.Coaching.Services;

public class ProgramService
{
    private readonly CoachingDbContext _db;
    private readonly ITenantContext _tenant;

    public ProgramService(CoachingDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<Domain.Program>> ListAsync(CancellationToken ct = default) =>
        await _db.Programs.AsNoTracking()
            .Include(p => p.Blocks).ThenInclude(b => b.Workouts).ThenInclude(w => w.Exercises)
            .OrderByDescending(p => p.Id)
            .ToListAsync(ct);

    public async Task<Domain.Program?> GetAsync(Guid id, CancellationToken ct = default) =>
        await _db.Programs.AsNoTracking()
            .Include(p => p.Blocks).ThenInclude(b => b.Workouts).ThenInclude(w => w.Exercises)
            .FirstOrDefaultAsync(p => p.Id == id, ct);

    public async Task<Domain.Program> CreateAsync(string name, bool isTemplate, Guid coachId, CancellationToken ct = default)
    {
        var program = new Domain.Program
        {
            Name = name,
            IsTemplate = isTemplate,
            CreatedByCoachId = coachId,
            TenantId = _tenant.TenantId
        };
        _db.Programs.Add(program);
        await _db.SaveChangesAsync(ct);
        return program;
    }

    public async Task<Block> AddBlockAsync(Guid programId, string name, int weekNumber, CancellationToken ct = default)
    {
        var program = await _db.Programs
            .Include(p => p.Blocks)
            .FirstOrDefaultAsync(p => p.Id == programId, ct)
            ?? throw new KeyNotFoundException($"Program {programId} not found.");

        var block = program.AddBlock(name, weekNumber);
        _db.Blocks.Add(block);
        await _db.SaveChangesAsync(ct);
        return block;
    }

    public async Task<Workout> AddWorkoutAsync(Guid blockId, string name, DayOfWeek dayOfWeek, CancellationToken ct = default)
    {
        var block = await _db.Blocks
            .Include(b => b.Workouts)
            .FirstOrDefaultAsync(b => b.Id == blockId, ct)
            ?? throw new KeyNotFoundException($"Block {blockId} not found.");

        var workout = block.AddWorkout(name, dayOfWeek);
        _db.Workouts.Add(workout);
        await _db.SaveChangesAsync(ct);
        return workout;
    }

    public async Task<WorkoutExercise> AddExerciseToWorkoutAsync(
        Guid workoutId, Guid exerciseId, int setCount, int repTarget,
        decimal? weightTarget, decimal? rpeTarget, int order, CancellationToken ct = default)
    {
        var workout = await _db.Workouts
            .Include(w => w.Exercises)
            .FirstOrDefaultAsync(w => w.Id == workoutId, ct)
            ?? throw new KeyNotFoundException($"Workout {workoutId} not found.");

        var we = workout.AddExercise(exerciseId, setCount, repTarget, weightTarget, rpeTarget, order);
        _db.WorkoutExercises.Add(we);
        await _db.SaveChangesAsync(ct);
        return we;
    }
}
