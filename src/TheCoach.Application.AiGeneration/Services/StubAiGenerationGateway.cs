using TheCoach.Application.AiGeneration.Domain;

namespace TheCoach.Application.AiGeneration.Services;

public sealed class StubAiGenerationGateway : IAiGenerationGateway
{
    public Task<WorkoutProgramDraft> GenerateWorkoutProgramAsync(
        WorkoutGenerationRequest request,
        CancellationToken ct = default)
    {
        var exercises = request.AvailableExerciseNames.Length >= 6
            ? request.AvailableExerciseNames[..6]
            : ["Barbell Bench Press", "Barbell Row", "Barbell Overhead Press",
               "Squat", "Romanian Deadlift", "Pull-Up"];

        var draft = new WorkoutProgramDraft(
            Name: $"AI-Generated {request.Goal} Program",
            Description: $"{request.DurationWeeks}-week mesocycle, {request.DaysPerWeek} days/week. Goal: {request.Goal}.",
            Blocks: [
                new DraftBlock("Accumulation", "Phase 1", 1, [
                    new DraftWorkout("Push A", [
                        new DraftExercise(exercises[0], 4, 8, 90, null),
                        new DraftExercise(exercises[2], 3, 10, 60, null)
                    ]),
                    new DraftWorkout("Pull A", [
                        new DraftExercise(exercises[1], 4, 8, 90, null),
                        new DraftExercise(exercises[5], 3, 10, 60, null)
                    ])
                ]),
                new DraftBlock("Intensification", "Phase 2", 2, [
                    new DraftWorkout("Push B", [
                        new DraftExercise(exercises[0], 5, 5, 120, "Increase weight by 5% vs Phase 1"),
                        new DraftExercise(exercises[2], 4, 6, 90, null)
                    ]),
                    new DraftWorkout("Pull B", [
                        new DraftExercise(exercises[1], 5, 5, 120, null),
                        new DraftExercise(exercises[4], 3, 10, 60, null)
                    ])
                ])
            ]);

        return Task.FromResult(draft);
    }

    public Task<MealPlanDraft> GenerateMealPlanAsync(
        MealPlanGenerationRequest request,
        CancellationToken ct = default)
    {
        var mealsPerDay = 3;
        var caloriesPerMeal = Math.Round(request.CalorieTarget / mealsPerDay, 0);
        var proteinPerMeal = Math.Round(request.ProteinGrams / mealsPerDay, 1);
        var carbsPerMeal = Math.Round(request.CarbGrams / mealsPerDay, 1);
        var fatPerMeal = Math.Round(request.FatGrams / mealsPerDay, 1);

        string[] dayNames = ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

        var days = dayNames.Select(day => new DraftDay(day, [
            new DraftMeal("Breakfast", ["Oats 100g", "Whey protein 30g", "Blueberries 50g"],
                caloriesPerMeal, proteinPerMeal, carbsPerMeal, fatPerMeal, null),
            new DraftMeal("Lunch", ["Chicken breast 150g", "Brown rice 120g", "Broccoli 100g"],
                caloriesPerMeal, proteinPerMeal, carbsPerMeal, fatPerMeal,
                "Grill chicken, steam rice and broccoli. Season with salt and olive oil."),
            new DraftMeal("Dinner", ["Salmon 150g", "Sweet potato 150g", "Asparagus 100g"],
                caloriesPerMeal, proteinPerMeal, carbsPerMeal, fatPerMeal,
                "Bake salmon at 200°C for 15 min. Roast sweet potato and asparagus.")
        ])).ToArray();

        var draft = new MealPlanDraft(
            Name: $"AI-Generated {request.CalorieTarget} kcal Meal Plan",
            Description: $"7-day meal plan: {request.CalorieTarget} kcal, {request.ProteinGrams}g protein.",
            Days: days,
            ShoppingList: [
                "Oats 700g", "Whey protein 210g", "Blueberries 350g",
                "Chicken breast 1050g", "Brown rice 840g", "Broccoli 700g",
                "Salmon 1050g", "Sweet potato 1050g", "Asparagus 700g",
                "Olive oil", "Sea salt"
            ]);

        return Task.FromResult(draft);
    }
}
