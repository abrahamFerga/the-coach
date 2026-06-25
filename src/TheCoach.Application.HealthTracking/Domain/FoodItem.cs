using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.HealthTracking.Domain;

public class FoodItem : TenantScopedEntity
{
    public string Name { get; set; } = string.Empty;
    public decimal CaloriesPer100g { get; set; }
    public decimal ProteinPer100g { get; set; }
    public decimal CarbPer100g { get; set; }
    public decimal FatPer100g { get; set; }
    public string? Barcode { get; set; }
    public FoodSource Source { get; set; } = FoodSource.Global;
}

public enum FoodSource { Global, USDA, Custom }
