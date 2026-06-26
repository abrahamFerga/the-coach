using System.Net;
using Aspire.Hosting;
using Aspire.Hosting.Testing;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace TheCoach.Tests.Smoke;

/// <summary>
/// Boots the whole system through the Aspire AppHost (real Postgres container) and
/// proves it actually runs — not just that it compiles. Doubles as the regression
/// test guarding the backbone wiring: remove the AppHost's Postgres/project wiring
/// or the API's startup schema creation and this goes red.
/// </summary>
public class AppHostSmokeTests
{
    [Fact]
    public async Task System_boots_serves_health_and_enforces_auth()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.TheCoach_AppHost>();
        appHost.Services.ConfigureHttpClientDefaults(http => http.AddStandardResilienceHandler());

        await using var app = await appHost.BuildAsync();
        await app.StartAsync();

        // Healthy only after Postgres is up, all 8 schemas are created, and the
        // startup seed has run (seeds complete before the HTTP server listens).
        await app.ResourceNotifications
            .WaitForResourceHealthyAsync("api")
            .WaitAsync(TimeSpan.FromSeconds(240));

        var client = app.CreateHttpClient("api");

        var health = await client.GetAsync("/health");
        health.StatusCode.Should().Be(HttpStatusCode.OK);

        // The security contract stays on: an unauthenticated call to an authorized
        // endpoint is rejected. A 401 here is correct behaviour, not a failure.
        var authed = await client.GetAsync("/api/v1/automations");
        authed.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
