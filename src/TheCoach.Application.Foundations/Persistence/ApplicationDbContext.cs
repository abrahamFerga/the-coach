using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Foundations.Domain;
using TheCoach.Application.Foundations.MultiTenancy;

namespace TheCoach.Application.Foundations.Persistence;

public abstract class ApplicationDbContext : DbContext
{
    protected readonly ITenantContext TenantContext;

    protected ApplicationDbContext(DbContextOptions options, ITenantContext tenantContext)
        : base(options)
    {
        TenantContext = tenantContext;
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        ApplyConventions(modelBuilder);
    }

    protected void ApplyConventions(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            var isTenantScoped = typeof(ITenantScoped).IsAssignableFrom(entityType.ClrType);
            var isSoftDeletable = typeof(ISoftDeletable).IsAssignableFrom(entityType.ClrType);

            if (isTenantScoped || isSoftDeletable)
            {
                var method = typeof(ApplicationDbContext)
                    .GetMethod(nameof(ApplyFilters),
                        System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)!
                    .MakeGenericMethod(entityType.ClrType);
                method.Invoke(this, [modelBuilder, isTenantScoped, isSoftDeletable]);
            }
        }
    }

    private void ApplyFilters<TEntity>(ModelBuilder modelBuilder, bool tenantScoped, bool softDeletable)
        where TEntity : class
    {
        if (tenantScoped && softDeletable)
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
                !((ISoftDeletable)e).IsDeleted &&
                (TenantContext.IsSystemAdmin ||
                 ((ITenantScoped)e).TenantId == Guid.Empty ||
                 ((ITenantScoped)e).TenantId == TenantContext.TenantId));
        }
        else if (tenantScoped)
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
                TenantContext.IsSystemAdmin ||
                ((ITenantScoped)e).TenantId == Guid.Empty ||
                ((ITenantScoped)e).TenantId == TenantContext.TenantId);
        }
        else if (softDeletable)
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(e => !((ISoftDeletable)e).IsDeleted);
        }
    }
}
