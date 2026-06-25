using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.HealthTracking.Domain;
using TheCoach.Application.HealthTracking.Persistence;

namespace TheCoach.Application.HealthTracking.Services;

public record MetricWindow(DateOnly From, DateOnly To);
public record PersonalBest(string Metric, decimal Value, DateOnly AchievedOn);

public class BodyMetricService
{
    private readonly HealthTrackingDbContext _db;
    private readonly ITenantContext _tenant;

    public BodyMetricService(HealthTrackingDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<BodyMetric> LogAsync(
        Guid clientId, DateOnly date,
        decimal? weightKg, decimal? bodyFatPercent,
        Dictionary<string, decimal>? measurements,
        string? photoBlobUrl,
        CancellationToken ct = default)
    {
        var metric = await _db.BodyMetrics
            .FirstOrDefaultAsync(m => m.ClientId == clientId && m.RecordedOn == date, ct);

        if (metric is null)
        {
            metric = new BodyMetric { ClientId = clientId, RecordedOn = date, TenantId = _tenant.TenantId };
            _db.BodyMetrics.Add(metric);
        }

        if (weightKg.HasValue) metric.WeightKg = weightKg;
        if (bodyFatPercent.HasValue) metric.BodyFatPercent = bodyFatPercent;
        if (measurements is not null) metric.SetMeasurements(measurements);
        if (photoBlobUrl is not null) metric.PhotoBlobUrl = photoBlobUrl;

        await _db.SaveChangesAsync(ct);
        return metric;
    }

    public async Task<List<BodyMetric>> GetTrendAsync(Guid clientId, MetricWindow window, CancellationToken ct = default) =>
        await _db.BodyMetrics.AsNoTracking()
            .Where(m => m.ClientId == clientId && m.RecordedOn >= window.From && m.RecordedOn <= window.To)
            .OrderBy(m => m.RecordedOn)
            .ToListAsync(ct);

    public async Task<List<PersonalBest>> GetPersonalBestsAsync(Guid clientId, CancellationToken ct = default)
    {
        var all = await _db.BodyMetrics.AsNoTracking()
            .Where(m => m.ClientId == clientId)
            .ToListAsync(ct);

        var pbs = new List<PersonalBest>();

        var bestWeight = all.Where(m => m.WeightKg.HasValue)
            .MinBy(m => m.WeightKg!.Value);
        if (bestWeight is not null)
            pbs.Add(new PersonalBest("weight_kg", bestWeight.WeightKg!.Value, bestWeight.RecordedOn));

        var bestBf = all.Where(m => m.BodyFatPercent.HasValue)
            .MinBy(m => m.BodyFatPercent!.Value);
        if (bestBf is not null)
            pbs.Add(new PersonalBest("body_fat_pct", bestBf.BodyFatPercent!.Value, bestBf.RecordedOn));

        var measurementKeys = all
            .SelectMany(m => m.GetMeasurements().Keys)
            .Distinct();

        foreach (var key in measurementKeys)
        {
            var best = all
                .Select(m => new { m.RecordedOn, Val = m.GetMeasurements().GetValueOrDefault(key, -1m) })
                .Where(x => x.Val >= 0)
                .MinBy(x => x.Val);
            if (best is not null)
                pbs.Add(new PersonalBest(key, best.Val, best.RecordedOn));
        }

        return pbs;
    }
}
