using Microsoft.EntityFrameworkCore;
using TheCoach.Application.CheckIns.Persistence;
using TheCoach.Application.Coaching.Persistence;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Workers.Jobs;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddDbContext<CoachingDbContext>((sp, opts) =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("coaching")));

builder.Services.AddDbContext<CheckInsDbContext>((sp, opts) =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("checkins")));

builder.Services.AddSingleton<ITenantContext, NullTenantContext>();
builder.Services.AddHostedService<ComplianceAlertScanner>();
builder.Services.AddHostedService<CheckInSchedulerJob>();

var host = builder.Build();
host.Run();

sealed class NullTenantContext : ITenantContext
{
    public Guid TenantId => Guid.Empty;
    public string TenantSlug => string.Empty;
    public string PlanTier => "free";
    public bool IsSystemAdmin => true;
}
