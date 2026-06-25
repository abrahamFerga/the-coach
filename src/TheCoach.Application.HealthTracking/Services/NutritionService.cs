using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.HealthTracking.Domain;
using TheCoach.Application.HealthTracking.Persistence;

namespace TheCoach.Application.HealthTracking.Services;

public class NutritionService
{
    private readonly HealthTrackingDbContext _db;
    private readonly ITenantContext _tenant;

    public NutritionService(HealthTrackingDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<NutritionTarget> SetTargetAsync(
        Guid clientId, Guid coachId,
        int calories, int protein, int carbs, int fat,
        CancellationToken ct = default)
    {
        var target = new NutritionTarget
        {
            ClientId = clientId,
            SetByCoachId = coachId,
            CalorieTarget = calories,
            ProteinGrams = protein,
            CarbGrams = carbs,
            FatGrams = fat,
            EffectiveDate = DateOnly.FromDateTime(DateTime.UtcNow),
            TenantId = _tenant.TenantId
        };
        _db.NutritionTargets.Add(target);
        await _db.SaveChangesAsync(ct);
        return target;
    }

    public async Task<NutritionTarget?> GetCurrentTargetAsync(Guid clientId, CancellationToken ct = default) =>
        await _db.NutritionTargets.AsNoTracking()
            .Where(t => t.ClientId == clientId)
            .OrderByDescending(t => t.EffectiveDate)
            .FirstOrDefaultAsync(ct);

    public async Task<NutritionLog> LogMealAsync(
        Guid clientId, DateOnly date,
        IEnumerable<LoggedFoodItem> items, CancellationToken ct = default)
    {
        var log = await _db.NutritionLogs
            .FirstOrDefaultAsync(l => l.ClientId == clientId && l.LogDate == date, ct);

        if (log is null)
        {
            log = new NutritionLog { ClientId = clientId, LogDate = date, TenantId = _tenant.TenantId };
            _db.NutritionLogs.Add(log);
        }

        log.SetFoodItems(items);
        await _db.SaveChangesAsync(ct);
        return log;
    }

    public async Task<DailySummary?> GetDailySummaryAsync(Guid clientId, DateOnly date, CancellationToken ct = default)
    {
        var log = await _db.NutritionLogs.AsNoTracking()
            .FirstOrDefaultAsync(l => l.ClientId == clientId && l.LogDate == date, ct);
        if (log is null) return null;

        var target = await GetCurrentTargetAsync(clientId, ct);
        var foodIds = log.GetFoodItems().Select(f => f.FoodItemId).ToList();
        var foods = await _db.FoodItems.AsNoTracking()
            .Where(f => foodIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, ct);

        var totals = log.ComputeTotals(foods);
        return new DailySummary(date, totals, target);
    }

    public async Task<List<DailySummary>> GetTrendAsync(Guid clientId, int days, CancellationToken ct = default)
    {
        var from = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
        var logs = await _db.NutritionLogs.AsNoTracking()
            .Where(l => l.ClientId == clientId && l.LogDate >= from)
            .OrderBy(l => l.LogDate)
            .ToListAsync(ct);

        var target = await GetCurrentTargetAsync(clientId, ct);
        var allFoodIds = logs.SelectMany(l => l.GetFoodItems().Select(f => f.FoodItemId)).Distinct().ToList();
        var foods = await _db.FoodItems.AsNoTracking()
            .Where(f => allFoodIds.Contains(f.Id))
            .ToDictionaryAsync(f => f.Id, ct);

        return logs.Select(l => new DailySummary(l.LogDate, l.ComputeTotals(foods), target)).ToList();
    }
}

public record DailySummary(DateOnly Date, MacroTotals Consumed, NutritionTarget? Target);
