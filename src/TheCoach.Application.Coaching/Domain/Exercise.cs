using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.Coaching.Domain;

public class Exercise : TenantScopedEntity, ISoftDeletable
{
    public string Name { get; set; } = string.Empty;
    public string MuscleGroup { get; set; } = string.Empty;
    public string? DemoVideoUrl { get; set; }
    public string Tags { get; set; } = string.Empty;
    public bool IsGlobal => TenantId == Guid.Empty;
    public bool IsDeleted { get; private set; }

    public void SoftDelete() => IsDeleted = true;
}
