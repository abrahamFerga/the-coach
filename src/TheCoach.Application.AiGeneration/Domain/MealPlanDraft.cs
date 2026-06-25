namespace TheCoach.Application.AiGeneration.Domain;

public record MealPlanDraft(
    string Name,
    string Description,
    DraftDay[] Days,
    string[] ShoppingList);

public record DraftDay(
    string DayName,
    DraftMeal[] Meals);

public record DraftMeal(
    string Name,
    string[] Ingredients,
    decimal Calories,
    decimal ProteinGrams,
    decimal CarbGrams,
    decimal FatGrams,
    string? Recipe);
