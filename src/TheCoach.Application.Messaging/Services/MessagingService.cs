using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.Messaging.Domain;
using TheCoach.Application.Messaging.Persistence;

namespace TheCoach.Application.Messaging.Services;

public record MessagePage(IReadOnlyList<Message> Messages, Guid? NextCursor);

public class MessagingService
{
    private readonly MessagingDbContext _db;
    private readonly ITenantContext _tenant;

    public MessagingService(MessagingDbContext db, ITenantContext tenant)
    {
        _db = db;
        _tenant = tenant;
    }

    public async Task<MessageThread> CreateThreadAsync(
        Guid coachId,
        IEnumerable<Guid> participantIds,
        ConversationType type,
        string? name = null,
        CancellationToken ct = default)
    {
        var thread = new MessageThread
        {
            Type = type,
            Name = name,
            CoachId = coachId,
            TenantId = _tenant.TenantId
        };
        var participants = participantIds.ToList();
        if (!participants.Contains(coachId)) participants.Add(coachId);
        thread.SetParticipants(participants);

        _db.MessageThreads.Add(thread);
        await _db.SaveChangesAsync(ct);
        return thread;
    }

    public async Task<List<MessageThread>> GetThreadsForUserAsync(
        Guid userId,
        CancellationToken ct = default)
    {
        var all = await _db.MessageThreads.AsNoTracking().ToListAsync(ct);
        return all.Where(t => t.HasParticipant(userId)).ToList();
    }

    public async Task<Message> SendTextMessageAsync(
        Guid threadId,
        Guid senderId,
        string body,
        CancellationToken ct = default)
    {
        await EnsureParticipantAsync(threadId, senderId, ct);

        var message = new Message
        {
            ThreadId = threadId,
            SenderId = senderId,
            Body = body,
            MessageType = MessageType.Text,
            TenantId = _tenant.TenantId
        };
        _db.Messages.Add(message);
        await _db.SaveChangesAsync(ct);
        return message;
    }

    public async Task<Message> SendVoiceMessageAsync(
        Guid threadId,
        Guid senderId,
        string voiceBlobUrl,
        CancellationToken ct = default)
    {
        await EnsureParticipantAsync(threadId, senderId, ct);

        var message = new Message
        {
            ThreadId = threadId,
            SenderId = senderId,
            VoiceBlobUrl = voiceBlobUrl,
            MessageType = MessageType.Voice,
            TenantId = _tenant.TenantId
        };
        _db.Messages.Add(message);
        await _db.SaveChangesAsync(ct);
        return message;
    }

    public async Task<MessagePage> GetMessagesAsync(
        Guid threadId,
        Guid requestingUserId,
        int pageSize = 50,
        Guid? cursorId = null,
        CancellationToken ct = default)
    {
        await EnsureParticipantAsync(threadId, requestingUserId, ct);

        var query = _db.Messages.AsNoTracking()
            .Where(m => m.ThreadId == threadId)
            .OrderByDescending(m => m.SentAt)
            .ThenByDescending(m => m.Id)
            .AsQueryable();

        if (cursorId.HasValue)
        {
            var cursor = await _db.Messages.AsNoTracking()
                .FirstOrDefaultAsync(m => m.Id == cursorId.Value, ct);
            if (cursor is not null)
                query = query.Where(m =>
                    m.SentAt < cursor.SentAt ||
                    (m.SentAt == cursor.SentAt && m.Id.CompareTo(cursor.Id) <= 0));
        }

        var messages = await query.Take(pageSize + 1).ToListAsync(ct);
        Guid? nextCursor = null;
        if (messages.Count > pageSize)
        {
            nextCursor = messages[pageSize].Id;
            messages = messages.Take(pageSize).ToList();
        }

        return new MessagePage(messages, nextCursor);
    }

    public async Task<int> MarkReadAsync(
        Guid threadId,
        Guid userId,
        DateTimeOffset upToTime,
        CancellationToken ct = default)
    {
        var messages = await _db.Messages
            .Where(m => m.ThreadId == threadId
                     && m.SenderId != userId
                     && m.SentAt <= upToTime)
            .ToListAsync(ct);

        var marked = 0;
        foreach (var msg in messages)
        {
            if (msg.GetReadReceipts().Any(r => r.UserId == userId)) continue;
            msg.MarkRead(userId);
            marked++;
        }

        if (marked > 0)
            await _db.SaveChangesAsync(ct);

        return marked;
    }

    private async Task EnsureParticipantAsync(Guid threadId, Guid userId, CancellationToken ct)
    {
        var thread = await _db.MessageThreads.AsNoTracking()
            .FirstOrDefaultAsync(t => t.Id == threadId, ct)
            ?? throw new InvalidOperationException($"Thread {threadId} not found.");

        if (!thread.HasParticipant(userId))
            throw new InvalidOperationException($"User {userId} is not a participant of thread {threadId}.");
    }
}
