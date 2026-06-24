using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.Coaching.Domain;

public class ProgramAssignment : TenantScopedEntity
{
    public Guid ProgramId { get; set; }
    public Guid ClientId { get; set; }
    public DateOnly StartDate { get; set; }
    public AssignmentStatus Status { get; set; } = AssignmentStatus.Active;
}

public enum AssignmentStatus { Active, Paused, Completed }
