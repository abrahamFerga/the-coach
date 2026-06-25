using System.Security.Claims;
using TheCoach.Application.Foundations.Auth;
using TheCoach.Application.Messaging.Domain;
using TheCoach.Application.Messaging.Services;

namespace TheCoach.Api.Endpoints;

public static class MessagingEndpoints
{
    public static IEndpointRouteBuilder MapMessagingEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/api/v1/messaging")
            .RequireAuthorization()
            .WithTags("Messaging");

        group.MapPost("/threads", async (
            CreateThreadRequest req,
            MessagingService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var coachId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var thread = await svc.CreateThreadAsync(coachId, req.ParticipantIds, req.Type, req.Name, ct);
            return Results.Ok(MapThread(thread));
        }).RequireAuthorization(Policies.MessagingManage);

        group.MapGet("/threads", async (
            MessagingService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var threads = await svc.GetThreadsForUserAsync(userId, ct);
            return Results.Ok(threads.Select(MapThread));
        }).RequireAuthorization(Policies.MessagingViewOwn);

        group.MapPost("/threads/{threadId:guid}/messages", async (
            Guid threadId,
            SendMessageRequest req,
            MessagingService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var senderId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var message = req.VoiceBlobUrl is not null
                ? await svc.SendVoiceMessageAsync(threadId, senderId, req.VoiceBlobUrl, ct)
                : await svc.SendTextMessageAsync(threadId, senderId, req.Body!, ct);
            return Results.Ok(MapMessage(message));
        }).RequireAuthorization(Policies.MessagingViewOwn);

        group.MapGet("/threads/{threadId:guid}/messages", async (
            Guid threadId,
            int pageSize,
            Guid? cursor,
            MessagingService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var page = await svc.GetMessagesAsync(threadId, userId, pageSize <= 0 ? 50 : pageSize, cursor, ct);
            return Results.Ok(new
            {
                Messages = page.Messages.Select(MapMessage),
                page.NextCursor
            });
        }).RequireAuthorization(Policies.MessagingViewOwn);

        group.MapPost("/threads/{threadId:guid}/read", async (
            Guid threadId,
            MarkReadRequest req,
            MessagingService svc,
            ClaimsPrincipal user,
            CancellationToken ct) =>
        {
            var userId = Guid.Parse(user.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var marked = await svc.MarkReadAsync(threadId, userId, req.UpToTime, ct);
            return Results.Ok(new { Marked = marked });
        }).RequireAuthorization(Policies.MessagingViewOwn);

        return app;
    }

    private static object MapThread(MessageThread t) => new
    {
        t.Id,
        t.Type,
        t.Name,
        t.CoachId,
        Participants = t.GetParticipants()
    };

    private static object MapMessage(Message m) => new
    {
        m.Id,
        m.ThreadId,
        m.SenderId,
        m.Body,
        m.VoiceBlobUrl,
        m.MessageType,
        m.SentAt,
        ReadBy = m.GetReadReceipts().Select(r => new { r.UserId, r.ReadAt })
    };

    private record CreateThreadRequest(
        ConversationType Type,
        string? Name,
        Guid[] ParticipantIds);

    private record SendMessageRequest(string? Body, string? VoiceBlobUrl);

    private record MarkReadRequest(DateTimeOffset UpToTime);
}
