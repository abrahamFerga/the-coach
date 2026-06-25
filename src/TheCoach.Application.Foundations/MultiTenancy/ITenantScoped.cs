namespace TheCoach.Application.Foundations.MultiTenancy;

public interface ITenantScoped
{
    Guid TenantId { get; set; }
}
