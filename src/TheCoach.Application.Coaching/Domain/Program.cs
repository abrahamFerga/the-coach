using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.Coaching.Domain;

public class Program : TenantScopedEntity
{
    public Guid CreatedByCoachId { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool IsTemplate { get; set; }
    public List<Block> Blocks { get; private set; } = [];

    public Block AddBlock(string name, int weekNumber)
    {
        var block = new Block { Name = name, WeekNumber = weekNumber, ProgramId = Id, TenantId = TenantId };
        Blocks.Add(block);
        return block;
    }
}

public class Block : TenantScopedEntity
{
    public Guid ProgramId { get; set; }
    public int WeekNumber { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<Workout> Workouts { get; private set; } = [];

    public Workout AddWorkout(string name, DayOfWeek dayOfWeek)
    {
        var workout = new Workout { Name = name, DayOfWeek = dayOfWeek, BlockId = Id, TenantId = TenantId };
        Workouts.Add(workout);
        return workout;
    }
}

public class Workout : TenantScopedEntity
{
    public Guid BlockId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    public string Name { get; set; } = string.Empty;
    public List<WorkoutExercise> Exercises { get; private set; } = [];

    public WorkoutExercise AddExercise(Guid exerciseId, int setCount, int repTarget, decimal? weightTarget = null, decimal? rpeTarget = null, int order = 0)
    {
        var we = new WorkoutExercise
        {
            ExerciseId = exerciseId,
            SetCount = setCount,
            RepTarget = repTarget,
            WeightTarget = weightTarget,
            RpeTarget = rpeTarget,
            Order = order,
            WorkoutId = Id,
            TenantId = TenantId
        };
        Exercises.Add(we);
        return we;
    }
}

public class WorkoutExercise : TenantScopedEntity
{
    public Guid WorkoutId { get; set; }
    public Guid ExerciseId { get; set; }
    public int SetCount { get; set; }
    public int RepTarget { get; set; }
    public decimal? WeightTarget { get; set; }
    public decimal? RpeTarget { get; set; }
    public int Order { get; set; }
}
