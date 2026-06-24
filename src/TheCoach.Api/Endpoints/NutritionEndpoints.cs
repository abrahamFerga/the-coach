using System.Security.Claims;
using Microsoft.AspNetCore.Mvc;
using TheCoach.Application.Foundations.Auth;
using TheCoach.Application.HealthTracking.Domain;
using TheCoach.Application.HealthTracking.Services;

namespace TheCoach.Api.Endpoints;

public static class NutritionEndpoints
{
    public static IEndpointRouteBuilder MapNutritionEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/nutrition")
            .RequireAuthorization()
            .WithTags("Nutrition");

        // Food database
        group.MapGet("/foods", async (
            [FromQuery] string? q,
            FoodDatabaseService svc,
            CancellationToken ct) =>
        {
            if (string.IsNullOrWhiteSpace(q)) return Results.BadRequest("query required");
            var items = await svc.SearchAsync(q, ct);
            return Results.Ok(items.Select(MapFood));
        });

        group.MapGet("/foods/barcode/{barcode}", async (
            string barcode,
            FoodDatabaseService svc,
            CancellationToken ct) =>
        {
            var item = await svc.GetByBarcodeAsync(barcode, ct);
            return item is null ? Results.NotFound() : Results.Ok(MapFood(item));
        });

        // Nutrition targets
        group.MapPost("/targets/client/{clientId:guid}", async (
            Guid clientId,
            SetTargetRequest req,
            NutritionService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var coachId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var target = await svc.SetTargetAsync(clientId, coachId, req.Calories, req.Protein, req.Carbs, req.Fat, ct);
            return Results.Created($"/api/v1/nutrition/targets/client/{clientId}", MapTarget(target));
        }).RequireAuthorization(Policies.HealthTrackingViewOwn);

        group.MapGet("/targets/client/{clientId:guid}", async (
            Guid clientId,
            NutritionService svc,
            CancellationToken ct) =>
        {
            var target = await svc.GetCurrentTargetAsync(clientId, ct);
            return target is null ? Results.NotFound() : Results.Ok(MapTarget(target));
        });

        // Nutrition logs
        group.MapPost("/logs", async (
            LogMealRequest req,
            NutritionService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var clientId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var log = await svc.LogMealAsync(clientId, req.Date,
                req.Items.Select(i => new LoggedFoodItem(i.FoodItemId, i.QuantityGrams)), ct);
            return Results.Ok(new { log.Id, log.LogDate });
        }).RequireAuthorization(Policies.HealthTrackingViewOwn);

        group.MapGet("/logs/summary/{clientId:guid}/{date}", async (
            Guid clientId,
            DateOnly date,
            NutritionService svc,
            CancellationToken ct) =>
        {
            var summary = await svc.GetDailySummaryAsync(clientId, date, ct);
            return summary is null ? Results.NotFound() : Results.Ok(MapSummary(summary));
        });

        group.MapGet("/logs/trend/{clientId:guid}", async (
            Guid clientId,
            [FromQuery] int days,
            NutritionService svc,
            CancellationToken ct) =>
        {
            var trend = await svc.GetTrendAsync(clientId, days > 0 ? days : 7, ct);
            return Results.Ok(trend.Select(MapSummary));
        });

        return app;
    }

    private static object MapFood(FoodItem f) => new
    {
        f.Id, f.Name, f.CaloriesPer100g, f.ProteinPer100g, f.CarbPer100g, f.FatPer100g, f.Barcode, f.Source
    };

    private static object MapTarget(Application.HealthTracking.Domain.NutritionTarget t) => new
    {
        t.Id, t.ClientId, t.CalorieTarget, t.ProteinGrams, t.CarbGrams, t.FatGrams, t.EffectiveDate
    };

    private static object MapSummary(Application.HealthTracking.Services.DailySummary s) => new
    {
        s.Date,
        Consumed = new { s.Consumed.Calories, s.Consumed.ProteinGrams, s.Consumed.CarbGrams, s.Consumed.FatGrams },
        Target = s.Target is null ? null : new
        {
            s.Target.CalorieTarget, s.Target.ProteinGrams, s.Target.CarbGrams, s.Target.FatGrams
        }
    };

    private record SetTargetRequest(int Calories, int Protein, int Carbs, int Fat);
    private record LoggedItemRequest(Guid FoodItemId, decimal QuantityGrams);
    private record LogMealRequest(DateOnly Date, List<LoggedItemRequest> Items);
}
