using System.Text.Json;
using TheCoach.Application.Foundations.Domain;

namespace TheCoach.Application.Messaging.Domain;

public enum MessageType { Text, Voice }

public record MessageReadReceipt(Guid UserId, DateTimeOffset ReadAt);

public class Message : TenantScopedEntity, ISoftDeletable
{
    public Guid ThreadId { get; set; }
    public Guid SenderId { get; set; }
    public string? Body { get; set; }
    public string? VoiceBlobUrl { get; set; }
    public MessageType MessageType { get; set; }
    public DateTimeOffset SentAt { get; set; } = DateTimeOffset.UtcNow;
    public bool IsDeleted { get; set; }
    public string ReadReceiptsJson { get; private set; } = "[]";

    public List<MessageReadReceipt> GetReadReceipts() =>
        JsonSerializer.Deserialize<List<MessageReadReceipt>>(ReadReceiptsJson) ?? [];

    public void MarkRead(Guid userId)
    {
        var receipts = GetReadReceipts();
        if (receipts.Any(r => r.UserId == userId)) return;
        receipts.Add(new MessageReadReceipt(userId, DateTimeOffset.UtcNow));
        ReadReceiptsJson = JsonSerializer.Serialize(receipts);
    }
}
