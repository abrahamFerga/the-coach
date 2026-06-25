using TheCoach.Application.AiGeneration.Domain;

namespace TheCoach.Application.AiGeneration.Services;

public record WorkoutGenerationRequest(
    string Goal,
    int DurationWeeks,
    int DaysPerWeek,
    string[] Equipment,
    string[] Restrictions,
    string[] AvailableExerciseNames);

public record MealPlanGenerationRequest(
    decimal CalorieTarget,
    decimal ProteinGrams,
    decimal CarbGrams,
    decimal FatGrams,
    string[] DietaryRestrictions);

public interface IAiGenerationGateway
{
    Task<WorkoutProgramDraft> GenerateWorkoutProgramAsync(
        WorkoutGenerationRequest request,
        CancellationToken ct = default);

    Task<MealPlanDraft> GenerateMealPlanAsync(
        MealPlanGenerationRequest request,
        CancellationToken ct = default);
}
