using System.Text.Json;
using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.Messaging.Domain;

public enum ConversationType { Direct, Group }

public class MessageThread : TenantScopedEntity
{
    public ConversationType Type { get; set; }
    public string? Name { get; set; }
    public Guid CoachId { get; set; }
    public string ParticipantIdsJson { get; private set; } = "[]";

    public List<Guid> GetParticipants() =>
        JsonSerializer.Deserialize<List<Guid>>(ParticipantIdsJson) ?? [];

    public void SetParticipants(IEnumerable<Guid> ids) =>
        ParticipantIdsJson = JsonSerializer.Serialize(ids.Distinct().ToList());

    public bool HasParticipant(Guid userId) =>
        GetParticipants().Contains(userId);
}
