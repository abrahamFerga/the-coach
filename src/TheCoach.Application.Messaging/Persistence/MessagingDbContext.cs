using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.Foundations.Persistence;
using TheCoach.Application.Messaging.Domain;

namespace TheCoach.Application.Messaging.Persistence;

public class MessagingDbContext : ApplicationDbContext
{
    public DbSet<MessageThread> MessageThreads => Set<MessageThread>();
    public DbSet<Message> Messages => Set<Message>();

    public MessagingDbContext(DbContextOptions<MessagingDbContext> options, ITenantContext tenantContext)
        : base(options, tenantContext)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<MessageThread>(b =>
        {
            b.ToTable("message_threads");
            b.Property(t => t.ParticipantIdsJson).HasColumnName("participant_ids");
            b.HasIndex(t => new { t.TenantId, t.CoachId });
        });

        modelBuilder.Entity<Message>(b =>
        {
            b.ToTable("messages");
            b.Property(m => m.ReadReceiptsJson).HasColumnName("read_receipts");
            b.HasIndex(m => new { m.ThreadId, m.SentAt });
        });
    }
}
