using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Coaching.Domain;
using TheCoach.Application.Coaching.Persistence;
using TheCoach.Application.Coaching.Services;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Tests.Integration;

public class ComplianceServiceTests
{
    private static (CoachingDbContext db, ComplianceService svc) Build(Guid tenantId, string dbName)
    {
        var options = new DbContextOptionsBuilder<CoachingDbContext>()
            .UseInMemoryDatabase(dbName)
            .Options;
        var tenant = new StubTenantContext(tenantId);
        var db = new CoachingDbContext(options, tenant);
        var svc = new ComplianceService(db, tenant);
        return (db, svc);
    }

    [Fact]
    public async Task Roster_returns_green_for_client_logged_today()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (db, svc) = Build(tenantId, dbName);

        db.ProgramAssignments.Add(new ProgramAssignment
        {
            Id = Guid.NewGuid(),
            ProgramId = programId,
            ClientId = clientId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-7)),
            Status = AssignmentStatus.Active,
            TenantId = tenantId
        });
        var log = new WorkoutLog { ClientId = clientId, TenantId = tenantId };
        log.SetSets([]);
        db.WorkoutLogs.Add(log);
        await db.SaveChangesAsync();

        var roster = await svc.GetRosterAsync();

        roster.Should().ContainSingle(r => r.ClientId == clientId && r.Status == AdherenceStatus.Green);
    }

    [Fact]
    public async Task Roster_returns_red_for_client_silent_for_5_days()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (db, svc) = Build(tenantId, dbName);

        db.ProgramAssignments.Add(new ProgramAssignment
        {
            Id = Guid.NewGuid(),
            ProgramId = programId,
            ClientId = clientId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-14)),
            Status = AssignmentStatus.Active,
            TenantId = tenantId
        });
        var staleLog = new WorkoutLog
        {
            ClientId = clientId,
            TenantId = tenantId,
            LoggedAt = DateTimeOffset.UtcNow.AddDays(-5)
        };
        staleLog.SetSets([]);
        db.WorkoutLogs.Add(staleLog);
        await db.SaveChangesAsync();

        var roster = await svc.GetRosterAsync();

        roster.Should().ContainSingle(r => r.ClientId == clientId && r.Status == AdherenceStatus.Red);
    }

    [Fact]
    public async Task ScanAndRaiseAlerts_creates_alert_for_silent_client()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (db, svc) = Build(tenantId, dbName);

        db.ProgramAssignments.Add(new ProgramAssignment
        {
            Id = Guid.NewGuid(),
            ProgramId = programId,
            ClientId = clientId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            Status = AssignmentStatus.Active,
            TenantId = tenantId
        });
        await db.SaveChangesAsync();

        var raised = await svc.ScanAndRaiseAlertsAsync();

        raised.Should().Be(1);
        var (db2, _) = Build(tenantId, dbName);
        var alerts = await db2.ComplianceAlerts.ToListAsync();
        alerts.Should().ContainSingle(a => a.ClientId == clientId && !a.IsAcknowledged);
    }

    [Fact]
    public async Task ScanAndRaiseAlerts_does_not_duplicate_open_alert()
    {
        var tenantId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var programId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (db, svc) = Build(tenantId, dbName);

        db.ProgramAssignments.Add(new ProgramAssignment
        {
            Id = Guid.NewGuid(),
            ProgramId = programId,
            ClientId = clientId,
            StartDate = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)),
            Status = AssignmentStatus.Active,
            TenantId = tenantId
        });
        db.ComplianceAlerts.Add(new ComplianceAlert
        {
            ClientId = clientId,
            CoachId = Guid.NewGuid(),
            TenantId = tenantId
        });
        await db.SaveChangesAsync();

        var raised = await svc.ScanAndRaiseAlertsAsync();

        raised.Should().Be(0, "should not create a duplicate open alert");
    }

    [Fact]
    public async Task AcknowledgeAlert_records_timestamp_and_coach()
    {
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var (db, svc) = Build(tenantId, dbName);

        var alert = new ComplianceAlert
        {
            ClientId = clientId,
            CoachId = coachId,
            TenantId = tenantId
        };
        db.ComplianceAlerts.Add(alert);
        await db.SaveChangesAsync();

        var result = await svc.AcknowledgeAsync(alert.Id, coachId);

        result.Should().NotBeNull();
        result!.IsAcknowledged.Should().BeTrue();
        result.AcknowledgedByCoachId.Should().Be(coachId);
    }

    private record StubTenantContext(Guid TenantId) : ITenantContext
    {
        public string TenantSlug => "stub";
        public string PlanTier => "pro";
        public bool IsSystemAdmin => false;
    }
}
