using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.HealthTracking.Domain;
using TheCoach.Application.HealthTracking.Persistence;

namespace TheCoach.Application.HealthTracking.Services;

public class FoodDatabaseService
{
    private readonly HealthTrackingDbContext _db;
    private readonly ITenantContext _tenant;

    public FoodDatabaseService(HealthTrackingDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<List<FoodItem>> SearchAsync(string query, CancellationToken ct = default) =>
        await _db.FoodItems.AsNoTracking()
            .Where(f => (f.TenantId == Guid.Empty || f.TenantId == _tenant.TenantId) &&
                        EF.Functions.ILike(f.Name, $"%{query}%"))
            .OrderBy(f => f.Name)
            .Take(30)
            .ToListAsync(ct);

    public async Task<FoodItem?> GetByBarcodeAsync(string barcode, CancellationToken ct = default) =>
        await _db.FoodItems.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Barcode == barcode, ct);

    public async Task<FoodItem> UpsertFromExternalAsync(
        string name, string barcode,
        decimal cal, decimal prot, decimal carb, decimal fat,
        FoodSource source,
        CancellationToken ct = default)
    {
        var existing = await _db.FoodItems.FirstOrDefaultAsync(f => f.Barcode == barcode, ct);
        if (existing is not null)
            return existing;

        var item = new FoodItem
        {
            Name = name,
            Barcode = barcode,
            CaloriesPer100g = cal,
            ProteinPer100g = prot,
            CarbPer100g = carb,
            FatPer100g = fat,
            Source = source,
            TenantId = Guid.Empty
        };
        _db.FoodItems.Add(item);
        await _db.SaveChangesAsync(ct);
        return item;
    }
}
