using System.Text.Json;
using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.HealthTracking.Domain;

public class BodyMetric : TenantScopedEntity
{
    public Guid ClientId { get; set; }
    public DateOnly RecordedOn { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? BodyFatPercent { get; set; }
    public string MeasurementsJson { get; private set; } = "{}";
    public string? PhotoBlobUrl { get; set; }

    public Dictionary<string, decimal> GetMeasurements() =>
        JsonSerializer.Deserialize<Dictionary<string, decimal>>(MeasurementsJson) ?? [];

    public void SetMeasurements(Dictionary<string, decimal> measurements) =>
        MeasurementsJson = JsonSerializer.Serialize(measurements);
}
