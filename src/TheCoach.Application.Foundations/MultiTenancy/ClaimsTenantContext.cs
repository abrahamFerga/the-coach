using System.Security.Claims;
using Microsoft.AspNetCore.Http;

namespace TheCoach.Application.Foundations.MultiTenancy;

public sealed class ClaimsTenantContext : ITenantContext
{
    public Guid TenantId { get; }
    public string TenantSlug { get; }
    public string PlanTier { get; }
    public bool IsSystemAdmin { get; }

    public ClaimsTenantContext(IHttpContextAccessor accessor)
    {
        var user = accessor.HttpContext?.User;
        var tenantClaim = user?.FindFirst("tenant_id")?.Value;
        TenantId = tenantClaim is not null ? Guid.Parse(tenantClaim) : Guid.Empty;
        TenantSlug = user?.FindFirst("tenant_slug")?.Value ?? string.Empty;
        PlanTier = user?.FindFirst("plan_tier")?.Value ?? "free";
        IsSystemAdmin = user?.IsInRole("SystemAdmin") ?? false;
    }
}
