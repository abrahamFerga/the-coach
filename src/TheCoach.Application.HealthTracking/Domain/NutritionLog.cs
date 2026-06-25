using System.Text.Json;
using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.HealthTracking.Domain;

public class NutritionLog : TenantScopedEntity
{
    public Guid ClientId { get; set; }
    public DateOnly LogDate { get; set; }
    public string FoodItemsJson { get; private set; } = "[]";

    public IReadOnlyList<LoggedFoodItem> GetFoodItems() =>
        JsonSerializer.Deserialize<List<LoggedFoodItem>>(FoodItemsJson) ?? [];

    public void SetFoodItems(IEnumerable<LoggedFoodItem> items) =>
        FoodItemsJson = JsonSerializer.Serialize(items);

    public MacroTotals ComputeTotals(IReadOnlyDictionary<Guid, FoodItem> foodMap)
    {
        var items = GetFoodItems();
        decimal cal = 0, prot = 0, carb = 0, fat = 0;
        foreach (var li in items)
        {
            if (!foodMap.TryGetValue(li.FoodItemId, out var fi)) continue;
            var factor = li.QuantityGrams / 100m;
            cal += fi.CaloriesPer100g * factor;
            prot += fi.ProteinPer100g * factor;
            carb += fi.CarbPer100g * factor;
            fat += fi.FatPer100g * factor;
        }
        return new MacroTotals(Math.Round(cal, 1), Math.Round(prot, 1), Math.Round(carb, 1), Math.Round(fat, 1));
    }
}

public record LoggedFoodItem(Guid FoodItemId, decimal QuantityGrams);

public record MacroTotals(decimal Calories, decimal ProteinGrams, decimal CarbGrams, decimal FatGrams);
