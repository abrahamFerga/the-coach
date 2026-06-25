using Microsoft.EntityFrameworkCore;
using TheCoach.Application.AiGeneration.Domain;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.Foundations.Persistence;

namespace TheCoach.Application.AiGeneration.Persistence;

public class AiGenerationDbContext : ApplicationDbContext
{
    public DbSet<AiDraft> AiDrafts => Set<AiDraft>();

    public AiGenerationDbContext(DbContextOptions<AiGenerationDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<AiDraft>(b =>
        {
            b.ToTable("ai_drafts");
            b.Property(d => d.DraftType).HasConversion<string>();
            b.Property(d => d.Status).HasConversion<string>();
            b.Property(d => d.ContentJson).HasColumnName("content");
            b.HasIndex(d => new { d.TenantId, d.CoachId, d.GeneratedAt });
        });
    }
}
