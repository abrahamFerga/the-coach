using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using TheCoach.Application.Foundations.MultiTenancy;
using TheCoach.Application.Messaging.Domain;
using TheCoach.Application.Messaging.Persistence;
using TheCoach.Application.Messaging.Services;

namespace TheCoach.Tests.Integration;

public class MessagingServiceTests
{
    private static (MessagingDbContext db, MessagingService svc) Build(Guid tenantId, string? dbName = null)
    {
        var options = new DbContextOptionsBuilder<MessagingDbContext>()
            .UseInMemoryDatabase(dbName ?? Guid.NewGuid().ToString())
            .Options;
        var tenant = new StubTenantContext(tenantId);
        var db = new MessagingDbContext(options, tenant);
        return (db, new MessagingService(db, tenant));
    }

    [Fact]
    public async Task CreateThread_includes_coach_as_participant()
    {
        var tenantId = Guid.NewGuid();
        var coachId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var (_, svc) = Build(tenantId);

        var thread = await svc.CreateThreadAsync(coachId, [clientId], ConversationType.Direct);

        thread.TenantId.Should().Be(tenantId);
        thread.GetParticipants().Should().Contain(coachId);
        thread.GetParticipants().Should().Contain(clientId);
    }

    [Fact]
    public async Task SendTextMessage_and_GetMessages_returns_message()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var coachId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        var (_, svc) = Build(tenantId, dbName);
        var thread = await svc.CreateThreadAsync(coachId, [clientId], ConversationType.Direct);
        await svc.SendTextMessageAsync(thread.Id, coachId, "Hello!");

        var (_, svc2) = Build(tenantId, dbName);
        var page = await svc2.GetMessagesAsync(thread.Id, coachId);

        page.Messages.Should().ContainSingle(m => m.Body == "Hello!" && m.MessageType == MessageType.Text);
    }

    [Fact]
    public async Task SendVoiceMessage_stores_blob_url()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var coachId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        var (_, svc) = Build(tenantId, dbName);
        var thread = await svc.CreateThreadAsync(coachId, [clientId], ConversationType.Direct);
        await svc.SendVoiceMessageAsync(thread.Id, coachId, "https://blob.core.windows.net/voices/note1.webm");

        var (_, svc2) = Build(tenantId, dbName);
        var page = await svc2.GetMessagesAsync(thread.Id, coachId);

        page.Messages.Should().ContainSingle(m =>
            m.MessageType == MessageType.Voice &&
            m.VoiceBlobUrl == "https://blob.core.windows.net/voices/note1.webm");
    }

    [Fact]
    public async Task GetMessages_paginates_with_cursor()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var coachId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        var (_, svc) = Build(tenantId, dbName);
        var thread = await svc.CreateThreadAsync(coachId, [clientId], ConversationType.Direct);

        for (var i = 1; i <= 5; i++)
            await svc.SendTextMessageAsync(thread.Id, coachId, $"Msg {i}");

        var (_, svc2) = Build(tenantId, dbName);
        var page1 = await svc2.GetMessagesAsync(thread.Id, coachId, pageSize: 3);
        page1.Messages.Should().HaveCount(3);
        page1.NextCursor.Should().NotBeNull();

        var (_, svc3) = Build(tenantId, dbName);
        var page2 = await svc3.GetMessagesAsync(thread.Id, coachId, pageSize: 3, cursorId: page1.NextCursor);
        page2.Messages.Should().HaveCount(2);
        page2.NextCursor.Should().BeNull();
    }

    [Fact]
    public async Task MarkReadAsync_records_receipts_for_other_sender_messages()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var coachId = Guid.NewGuid();
        var clientId = Guid.NewGuid();

        var (_, svc) = Build(tenantId, dbName);
        var thread = await svc.CreateThreadAsync(coachId, [clientId], ConversationType.Direct);
        await svc.SendTextMessageAsync(thread.Id, coachId, "Check this out");

        var (_, svc2) = Build(tenantId, dbName);
        var marked = await svc2.MarkReadAsync(thread.Id, clientId, DateTimeOffset.UtcNow);

        marked.Should().Be(1);

        var (_, svc3) = Build(tenantId, dbName);
        var page = await svc3.GetMessagesAsync(thread.Id, clientId);
        page.Messages[0].GetReadReceipts().Should().Contain(r => r.UserId == clientId);
    }

    [Fact]
    public async Task NonParticipant_cannot_read_messages()
    {
        var tenantId = Guid.NewGuid();
        var dbName = Guid.NewGuid().ToString();
        var coachId = Guid.NewGuid();
        var clientId = Guid.NewGuid();
        var strangeId = Guid.NewGuid();

        var (_, svc) = Build(tenantId, dbName);
        var thread = await svc.CreateThreadAsync(coachId, [clientId], ConversationType.Direct);

        var (_, svc2) = Build(tenantId, dbName);
        var act = async () => await svc2.GetMessagesAsync(thread.Id, strangeId);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    [Fact]
    public async Task Threads_are_isolated_by_tenant()
    {
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        var dbName = "msg-isolation";
        var coachA = Guid.NewGuid();
        var userA = Guid.NewGuid();

        var (_, svcA) = Build(tenantA, dbName);
        await svcA.CreateThreadAsync(coachA, [userA], ConversationType.Direct);

        var (_, svcB) = Build(tenantB, dbName);
        var threadsForB = await svcB.GetThreadsForUserAsync(userA);

        threadsForB.Should().BeEmpty("threads from tenantA must not be visible to tenantB");
    }

    private record StubTenantContext(Guid TenantId) : ITenantContext
    {
        public string TenantSlug => "stub";
        public string PlanTier => "pro";
        public bool IsSystemAdmin => false;
    }
}
