using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.CheckIns.Domain;

public class CheckInAssignment : TenantScopedEntity
{
    public Guid CheckInTemplateId { get; set; }
    public Guid ClientId { get; set; }
    public Guid CoachId { get; set; }
    public DateOnly StartsOn { get; set; }
    public bool IsActive { get; set; } = true;
}
