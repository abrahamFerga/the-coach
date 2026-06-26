var builder = DistributedApplication.CreateBuilder(args);

// One Postgres server; one logical database per bounded context. The connection
// names below are what TheCoach.Api / TheCoach.Workers resolve via
// builder.Configuration.GetConnectionString("<name>"). Physical database names
// avoid hyphens so they need no quoting.
var postgres = builder.AddPostgres("postgres")
    .WithDataVolume();

var coaching = postgres.AddDatabase("coaching");
var healthTracking = postgres.AddDatabase("health-tracking", "health_tracking");
var checkins = postgres.AddDatabase("checkins");
var messaging = postgres.AddDatabase("messaging");
var billing = postgres.AddDatabase("billing");
var aiGeneration = postgres.AddDatabase("ai-generation", "ai_generation");
var automations = postgres.AddDatabase("automations");
var athleteAnalytics = postgres.AddDatabase("athlete-analytics", "athlete_analytics");

var api = builder.AddProject<Projects.TheCoach_Api>("api")
    .WithReference(coaching)
    .WithReference(healthTracking)
    .WithReference(checkins)
    .WithReference(messaging)
    .WithReference(billing)
    .WithReference(aiGeneration)
    .WithReference(automations)
    .WithReference(athleteAnalytics)
    .WaitFor(postgres)
    .WithHttpHealthCheck("/health");

// Workers share the coaching / checkins / automations databases with the API.
// They wait for the API so the schema (created on API startup) exists first.
builder.AddProject<Projects.TheCoach_Workers>("workers")
    .WithReference(coaching)
    .WithReference(checkins)
    .WithReference(automations)
    .WaitFor(postgres)
    .WaitFor(api);

builder.Build().Run();
