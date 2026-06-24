using Microsoft.EntityFrameworkCore;
using TheCoach.Application.CheckIns.Domain;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.Foundations.Persistence;

namespace TheCoach.Application.CheckIns.Persistence;

public class CheckInsDbContext : ApplicationDbContext
{
    public DbSet<CheckInTemplate> CheckInTemplates => Set<CheckInTemplate>();
    public DbSet<CheckInAssignment> CheckInAssignments => Set<CheckInAssignment>();
    public DbSet<CheckInResponse> CheckInResponses => Set<CheckInResponse>();

    public CheckInsDbContext(DbContextOptions<CheckInsDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<CheckInTemplate>(b =>
        {
            b.ToTable("checkin_templates");
            b.Property(t => t.QuestionsJson).HasColumnName("questions");
            b.HasIndex(t => new { t.TenantId, t.Name });
        });

        modelBuilder.Entity<CheckInAssignment>(b =>
        {
            b.ToTable("checkin_assignments");
            b.HasIndex(a => new { a.TenantId, a.ClientId, a.CheckInTemplateId, a.IsActive });
        });

        modelBuilder.Entity<CheckInResponse>(b =>
        {
            b.ToTable("checkin_responses");
            b.HasIndex(r => new { r.AssignmentId, r.DueDate }).IsUnique();
            b.Property(r => r.AnswersJson).HasColumnName("answers");
        });
    }
}
