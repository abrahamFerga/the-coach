using Microsoft.EntityFrameworkCore;
using TheCoach.Application.AthleteAnalytics.Domain;
using TheCoach.Application.AthleteAnalytics.Persistence;
using TheCoach.Application.Coaching.Persistence;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Application.AthleteAnalytics.Services;

public record TrainingLoadResult(
    decimal Atl,
    decimal Ctl,
    decimal Tsb,
    DateOnly Through);

public class AthleteAnalyticsService
{
    private readonly AthleteAnalyticsDbContext _db;
    private readonly CoachingDbContext _coachingDb;
    private readonly ITenantContext _tenant;

    public AthleteAnalyticsService(
        AthleteAnalyticsDbContext db,
        CoachingDbContext coachingDb,
        ITenantContext tenant)
    {
        _db = db;
        _coachingDb = coachingDb;
        _tenant = tenant;
    }

    public async Task<ReadinessEntry> LogReadinessAsync(
        Guid athleteId,
        DateOnly date,
        int readinessScore,
        decimal? hrvMs = null,
        string? notes = null,
        CancellationToken ct = default)
    {
        if (readinessScore < 1 || readinessScore > 10)
            throw new ArgumentOutOfRangeException(nameof(readinessScore), "Must be 1–10.");

        var existing = await _db.ReadinessEntries
            .FirstOrDefaultAsync(r => r.AthleteId == athleteId && r.Date == date, ct);

        if (existing is not null)
        {
            existing.ReadinessScore = readinessScore;
            existing.HrvMs = hrvMs;
            existing.Notes = notes;
        }
        else
        {
            existing = new ReadinessEntry
            {
                AthleteId = athleteId,
                Date = date,
                ReadinessScore = readinessScore,
                HrvMs = hrvMs,
                Notes = notes,
                TenantId = _tenant.TenantId
            };
            _db.ReadinessEntries.Add(existing);
        }

        await _db.SaveChangesAsync(ct);
        return existing;
    }

    public async Task<List<ReadinessEntry>> GetReadinessTrendAsync(
        Guid athleteId,
        int days = 28,
        CancellationToken ct = default)
    {
        var cutoff = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-days));
        return await _db.ReadinessEntries.AsNoTracking()
            .Where(r => r.AthleteId == athleteId && r.Date >= cutoff)
            .OrderBy(r => r.Date)
            .ToListAsync(ct);
    }

    public async Task<TrainingLoadResult> GetTrainingLoadAsync(
        Guid athleteId,
        DateOnly? through = null,
        CancellationToken ct = default)
    {
        var toDate = through ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromDate = toDate.AddDays(-56);
        var fromOffset = new DateTimeOffset(fromDate.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero);
        var toOffset = new DateTimeOffset(toDate.ToDateTime(TimeOnly.MaxValue), TimeSpan.Zero);

        var logs = await _coachingDb.WorkoutLogs.AsNoTracking()
            .Where(l => l.ClientId == athleteId && l.LoggedAt >= fromOffset && l.LoggedAt <= toOffset)
            .ToListAsync(ct);

        // Daily load = sum(RPE * Reps) across all sets logged that day
        var dailyLoads = logs
            .GroupBy(l => DateOnly.FromDateTime(l.LoggedAt.UtcDateTime))
            .ToDictionary(
                g => g.Key,
                g => g.SelectMany(log => log.GetSets())
                       .Where(s => s.Rpe.HasValue)
                       .Sum(s => s.Rpe!.Value * s.Reps));

        decimal atl = 0, ctl = 0;
        for (var day = fromDate; day <= toDate; day = day.AddDays(1))
        {
            var load = dailyLoads.GetValueOrDefault(day, 0m);
            atl += (load - atl) / 7m;
            ctl += (load - ctl) / 42m;
        }

        return new TrainingLoadResult(
            Math.Round(atl, 2),
            Math.Round(ctl, 2),
            Math.Round(ctl - atl, 2),
            toDate);
    }

    public async Task<PeriodizationPlan> CreatePeriodizationPlanAsync(
        Guid coachId,
        Guid athleteId,
        string name,
        DateOnly startsOn,
        IEnumerable<Mesocycle> mesocycles,
        CancellationToken ct = default)
    {
        var plan = new PeriodizationPlan
        {
            CoachId = coachId,
            AthleteId = athleteId,
            Name = name,
            StartsOn = startsOn,
            TenantId = _tenant.TenantId
        };
        plan.SetMesocycles(mesocycles);
        _db.PeriodizationPlans.Add(plan);
        await _db.SaveChangesAsync(ct);
        return plan;
    }

    public async Task<List<PeriodizationPlan>> ListPeriodizationPlansAsync(
        Guid athleteId,
        CancellationToken ct = default) =>
        await _db.PeriodizationPlans.AsNoTracking()
            .Where(p => p.AthleteId == athleteId)
            .OrderByDescending(p => p.StartsOn)
            .ToListAsync(ct);

    public async Task<DrillResult> LogDrillResultAsync(
        Guid athleteId,
        string drillName,
        string metricName,
        decimal value,
        DateTimeOffset? loggedAt = null,
        string? notes = null,
        CancellationToken ct = default)
    {
        var result = new DrillResult
        {
            AthleteId = athleteId,
            DrillName = drillName,
            MetricName = metricName,
            Value = value,
            LoggedAt = loggedAt ?? DateTimeOffset.UtcNow,
            Notes = notes,
            TenantId = _tenant.TenantId
        };
        _db.DrillResults.Add(result);
        await _db.SaveChangesAsync(ct);
        return result;
    }

    public async Task<List<DrillResult>> GetDrillTrendAsync(
        Guid athleteId,
        string drillName,
        string metricName,
        int days = 90,
        CancellationToken ct = default)
    {
        var cutoff = DateTimeOffset.UtcNow.AddDays(-days);
        return await _db.DrillResults.AsNoTracking()
            .Where(d => d.AthleteId == athleteId
                     && d.DrillName == drillName
                     && d.MetricName == metricName
                     && d.LoggedAt >= cutoff)
            .OrderBy(d => d.LoggedAt)
            .ToListAsync(ct);
    }
}
