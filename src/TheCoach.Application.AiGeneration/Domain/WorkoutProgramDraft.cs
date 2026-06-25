namespace TheCoach.Application.AiGeneration.Domain;

public record WorkoutProgramDraft(
    string Name,
    string Description,
    DraftBlock[] Blocks);

public record DraftBlock(
    string Name,
    string Phase,
    int WeekNumber,
    DraftWorkout[] Workouts);

public record DraftWorkout(
    string Name,
    DraftExercise[] Exercises);

public record DraftExercise(
    string ExerciseName,
    int Sets,
    int Reps,
    int? RestSeconds,
    string? Notes);
