namespace TheCoach.Application.Foundations.MultiTenancy;

public interface ITenantContext
{
    Guid TenantId { get; }
    string TenantSlug { get; }
    string PlanTier { get; }
    bool IsSystemAdmin { get; }
}
