namespace TheCoach.Application.Foundations.Domain;

public interface ISoftDeletable
{
    bool IsDeleted { get; }
}

public abstract class EntityBase
{
    public Guid Id { get; set; } = NewId();

    private static Guid NewId() => Guid.CreateVersion7();
}

public abstract class TenantScopedEntity : EntityBase, MultiTenancy.ITenantScoped
{
    public Guid TenantId { get; set; }
}
