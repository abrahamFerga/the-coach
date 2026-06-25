using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Coaching.Domain;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.Foundations.Persistence;

namespace TheCoach.Application.Coaching.Persistence;

public class CoachingDbContext : ApplicationDbContext
{
    public DbSet<Domain.Program> Programs => Set<Domain.Program>();
    public DbSet<Block> Blocks => Set<Block>();
    public DbSet<Workout> Workouts => Set<Workout>();
    public DbSet<WorkoutExercise> WorkoutExercises => Set<WorkoutExercise>();
    public DbSet<Exercise> Exercises => Set<Exercise>();
    public DbSet<ProgramAssignment> ProgramAssignments => Set<ProgramAssignment>();
    public DbSet<WorkoutLog> WorkoutLogs => Set<WorkoutLog>();
    public DbSet<ComplianceAlert> ComplianceAlerts => Set<ComplianceAlert>();

    public CoachingDbContext(DbContextOptions<CoachingDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<Domain.Program>(b =>
        {
            b.ToTable("programs");
            b.HasIndex(p => new { p.TenantId, p.Name });
            b.HasMany(p => p.Blocks).WithOne().HasForeignKey(bl => bl.ProgramId);
        });

        modelBuilder.Entity<Block>(b =>
        {
            b.ToTable("blocks");
            b.HasMany(bl => bl.Workouts).WithOne().HasForeignKey(w => w.BlockId);
        });

        modelBuilder.Entity<Workout>(b =>
        {
            b.ToTable("workouts");
            b.HasMany(w => w.Exercises).WithOne().HasForeignKey(we => we.WorkoutId);
        });

        modelBuilder.Entity<WorkoutExercise>(b => b.ToTable("workout_exercises"));

        modelBuilder.Entity<Exercise>(b =>
        {
            b.ToTable("exercises");
            b.HasIndex(e => new { e.TenantId, e.Name });
        });

        modelBuilder.Entity<ProgramAssignment>(b =>
        {
            b.ToTable("program_assignments");
            b.HasIndex(pa => new { pa.TenantId, pa.ClientId, pa.Status });
        });

        modelBuilder.Entity<WorkoutLog>(b =>
        {
            b.ToTable("workout_logs");
            b.HasIndex(wl => new { wl.TenantId, wl.ClientId, wl.LoggedAt });
            b.Property(wl => wl.SetsJson).HasColumnName("sets");
        });

        modelBuilder.Entity<ComplianceAlert>(b =>
        {
            b.ToTable("compliance_alerts");
            b.HasIndex(ca => new { ca.TenantId, ca.CoachId, ca.ClientId, ca.AcknowledgedAt });
        });
    }
}
