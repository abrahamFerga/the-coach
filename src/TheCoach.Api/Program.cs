using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using TheCoach.Api.Endpoints;
using TheCoach.Application.Coaching.Persistence;
using TheCoach.Application.Coaching.Services;
using TheCoach.Application.Foundations.Auth;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.CheckIns.Persistence;
using TheCoach.Application.CheckIns.Services;
using TheCoach.Application.AiGeneration.Persistence;
using TheCoach.Application.AiGeneration.Services;
using TheCoach.Application.Billing.Persistence;
using TheCoach.Application.Billing.Services;
using TheCoach.Application.Messaging.Persistence;
using TheCoach.Application.Messaging.Services;
using TheCoach.Application.HealthTracking.Persistence;
using TheCoach.Application.HealthTracking.Services;
using TheCoach.Application.Automations.Persistence;
using TheCoach.Application.Automations.Services;
using TheCoach.Application.AthleteAnalytics.Persistence;
using TheCoach.Application.AthleteAnalytics.Services;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddOpenApi();
builder.Services.AddHttpContextAccessor();

builder.Services.AddScoped<ITenantContext, ClaimsTenantContext>();

builder.Services.AddDbContext<CoachingDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("coaching")));

builder.Services.AddScoped<ExerciseService>();
builder.Services.AddScoped<ProgramService>();
builder.Services.AddScoped<ProgramAssignmentService>();
builder.Services.AddScoped<WorkoutLogService>();
builder.Services.AddScoped<ComplianceService>();

builder.Services.AddDbContext<HealthTrackingDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("health-tracking")));
builder.Services.AddScoped<FoodDatabaseService>();
builder.Services.AddScoped<NutritionService>();
builder.Services.AddScoped<BodyMetricService>();

builder.Services.AddDbContext<CheckInsDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("checkins")));
builder.Services.AddScoped<CheckInService>();

builder.Services.AddDbContext<MessagingDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("messaging")));
builder.Services.AddScoped<MessagingService>();

builder.Services.AddDbContext<BillingDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("billing")));
builder.Services.AddScoped<IStripeGateway, NoOpStripeGateway>();
builder.Services.AddScoped<BillingService>();

builder.Services.AddDbContext<AiGenerationDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("ai-generation")));
builder.Services.AddScoped<IAiGenerationGateway, StubAiGenerationGateway>();
builder.Services.AddScoped<AiGenerationService>();

builder.Services.AddDbContext<AutomationsDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("automations")));
builder.Services.AddScoped<IAutomationActionDispatcher, LoggingActionDispatcher>();
builder.Services.AddScoped<AutomationService>();

builder.Services.AddDbContext<AthleteAnalyticsDbContext>(opts =>
    opts.UseNpgsql(builder.Configuration.GetConnectionString("athlete-analytics")));
builder.Services.AddScoped<AthleteAnalyticsService>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(opts =>
    {
        opts.Authority = builder.Configuration["Auth:Authority"];
        opts.Audience = builder.Configuration["Auth:Audience"];
    });

builder.Services.AddAuthorization(opts =>
{
    string[] coachRoles = [Roles.HeadCoach, Roles.Coach];
    string[] clientRoles = [Roles.Client, Roles.Athlete, Roles.Member];
    var allCoachingRoles = coachRoles.Concat(clientRoles).Concat([Roles.Operator]).ToArray();

    opts.AddPolicy(Policies.ProgramsCreate, p => p.RequireRole(coachRoles));
    opts.AddPolicy(Policies.ProgramsAssign, p => p.RequireRole(coachRoles));
    opts.AddPolicy(Policies.ProgramsViewAll, p => p.RequireRole(allCoachingRoles));
    opts.AddPolicy(Policies.ProgramsBrowseLibrary, p => p.RequireRole(allCoachingRoles));
    opts.AddPolicy(Policies.WorkoutLogsCreate, p => p.RequireRole(clientRoles));
    opts.AddPolicy(Policies.ComplianceViewOwn, p => p.RequireRole(allCoachingRoles));
    opts.AddPolicy(Policies.ComplianceViewAll, p => p.RequireRole([Roles.HeadCoach, Roles.Operator]));
    opts.AddPolicy(Policies.AIGenerate, p => p.RequireRole(coachRoles));
    opts.AddPolicy(Policies.BillingManage, p => p.RequireRole([Roles.Operator]));
    opts.AddPolicy(Policies.TenantsManage, p => p.RequireRole([Roles.SystemAdmin]));
    opts.AddPolicy(Policies.CheckInsManage, p => p.RequireRole(coachRoles));
    opts.AddPolicy(Policies.CheckInsViewOwn, p => p.RequireRole(allCoachingRoles));
    opts.AddPolicy(Policies.CheckInsViewAll, p => p.RequireRole(coachRoles));
    opts.AddPolicy(Policies.MessagingViewOwn, p => p.RequireRole(allCoachingRoles));
    opts.AddPolicy(Policies.MessagingManage, p => p.RequireRole(coachRoles));
    opts.AddPolicy(Policies.AutomationsManage, p => p.RequireRole(coachRoles));
    string[] athleteRoles = [Roles.Athlete, Roles.Coach, Roles.HeadCoach];
    opts.AddPolicy(Policies.AthleteAnalyticsLog, p => p.RequireRole(athleteRoles));
    opts.AddPolicy(Policies.AthleteAnalyticsView, p => p.RequireRole(athleteRoles));
    opts.AddPolicy(Policies.AthleteAnalyticsManage, p => p.RequireRole([Roles.HeadCoach, Roles.Coach]));
});

var app = builder.Build();

if (app.Environment.IsDevelopment())
    app.MapOpenApi();

app.UseAuthentication();
app.UseAuthorization();

app.MapExerciseEndpoints();
app.MapProgramEndpoints();
app.MapWorkoutLogEndpoints();
app.MapComplianceEndpoints();
app.MapNutritionEndpoints();
app.MapBodyMetricEndpoints();
app.MapCheckInEndpoints();
app.MapMessagingEndpoints();
app.MapBillingEndpoints();
app.MapAiGenerationEndpoints();
app.MapAutomationEndpoints();
app.MapAthleteAnalyticsEndpoints();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

await EnsureSchemasAsync(app);
await SeedExercisesIfNeeded(app);
await SeedCheckInTemplatesIfNeeded(app);

app.Run();

// No EF migrations are shipped yet; create each context's schema on startup so the
// system boots against a fresh Postgres. Each context owns a separate database, so
// there is no cross-context contention. Replace with migrations before production.
static async Task EnsureSchemasAsync(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var sp = scope.ServiceProvider;
    await sp.GetRequiredService<CoachingDbContext>().Database.EnsureCreatedAsync();
    await sp.GetRequiredService<HealthTrackingDbContext>().Database.EnsureCreatedAsync();
    await sp.GetRequiredService<CheckInsDbContext>().Database.EnsureCreatedAsync();
    await sp.GetRequiredService<MessagingDbContext>().Database.EnsureCreatedAsync();
    await sp.GetRequiredService<BillingDbContext>().Database.EnsureCreatedAsync();
    await sp.GetRequiredService<AiGenerationDbContext>().Database.EnsureCreatedAsync();
    await sp.GetRequiredService<AutomationsDbContext>().Database.EnsureCreatedAsync();
    await sp.GetRequiredService<AthleteAnalyticsDbContext>().Database.EnsureCreatedAsync();
}

static async Task SeedExercisesIfNeeded(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CoachingDbContext>();

    if (!await db.Exercises.AnyAsync())
    {
        db.Exercises.AddRange(TheCoach.Application.Coaching.Persistence.ExerciseSeed.GlobalExercises);
        await db.SaveChangesAsync();
    }
}

static async Task SeedCheckInTemplatesIfNeeded(WebApplication app)
{
    using var scope = app.Services.CreateScope();
    var db = scope.ServiceProvider.GetRequiredService<CheckInsDbContext>();

    if (!await db.CheckInTemplates.IgnoreQueryFilters().AnyAsync(t => t.IsBuiltIn))
    {
        db.CheckInTemplates.Add(TheCoach.Application.CheckIns.Persistence.CheckInTemplateSeed.WeeklyCheckIn());
        await db.SaveChangesAsync();
    }
}
