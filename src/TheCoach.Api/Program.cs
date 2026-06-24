using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using TheCoach.Api.Endpoints;
using TheCoach.Application.Coaching.Persistence;
using TheCoach.Application.Coaching.Services;
using TheCoach.Application.Foundations.Auth;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.HealthTracking.Persistence;
using TheCoach.Application.HealthTracking.Services;

var builder = WebApplication.CreateBuilder(args);

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

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));

await SeedExercisesIfNeeded(app);

app.Run();

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
