using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Domain.Notifications;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Redis;
using Datavanced.SmartTaskManagement.Infrastructure.LiveEvents;
using Datavanced.SmartTaskManagement.Infrastructure.Redis;

using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

using Role = Datavanced.SmartTaskManagement.Domain.Users.Role;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>Live events through a real Redis channel into the in-process registry.</summary>
[Collection(RedisCollectionDefinition.Name)]
public sealed class LiveEventFanOutTests(RedisFixture redis) : IAsyncLifetime
{
    private static readonly TimeSpan ReceiveTimeout = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan SilenceWindow = TimeSpan.FromMilliseconds(500);

    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;
    private readonly List<IConnectionMultiplexer> _connections = [];
    private readonly List<RedisLiveEventSubscriber> _subscribers = [];

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public async ValueTask DisposeAsync()
    {
        foreach (var subscriber in _subscribers)
        {
            await subscriber.StopAsync(CancellationToken.None);
        }

        foreach (var connection in _connections)
        {
            await connection.DisposeAsync();
        }
    }

    [Fact]
    public async Task Publish_ThroughRedis_ReachesEveryConnectionOfTheUser()
    {
        var (publisher, registry) = await CreateAsync(redis.ConnectionString);
        var userId = Guid.NewGuid();
        using var tabOne = registry.Open(userId, Role.TeamMember);
        using var tabTwo = registry.Open(userId, Role.TeamMember);
        var notification = new NotificationDto(
            Guid.NewGuid(), NotificationType.TaskAssigned, "msg", Guid.NewGuid(), null, DateTimeOffset.UtcNow, false);

        await publisher.PublishAsync([new LiveEvent(LiveEventTypes.Notification, [userId], false, notification)], _ct);

        foreach (var tab in new[] { tabOne, tabTwo })
        {
            var received = await ReadAsync(tab);
            received.Type.ShouldBe(LiveEventTypes.Notification);
            received.Notification.ShouldNotBeNull();
            received.Notification.Id.ShouldBe(notification.Id);
            received.Notification.Type.ShouldBe(NotificationType.TaskAssigned);
            received.Notification.TaskId.ShouldBe(notification.TaskId);
        }
    }

    [Fact]
    public async Task Publish_IncludeAdmins_ReachesAdminNotOtherUsers()
    {
        var (publisher, registry) = await CreateAsync(redis.ConnectionString);
        using var admin = registry.Open(Guid.NewGuid(), Role.Admin);
        using var member = registry.Open(Guid.NewGuid(), Role.TeamMember);

        await publisher.PublishAsync([new LiveEvent(LiveEventTypes.DashboardChanged, [], true, null)], _ct);

        var received = await ReadAsync(admin);
        received.Type.ShouldBe(LiveEventTypes.DashboardChanged);
        received.Notification.ShouldBeNull();
        using var silence = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        silence.CancelAfter(SilenceWindow);
        await Should.ThrowAsync<OperationCanceledException>(
            async () => await member.Events.ReadAsync(silence.Token));
    }

    [Fact]
    public async Task Publish_RedisDown_DoesNotThrow()
    {
        var (publisher, _) = await CreateAsync("127.0.0.1:1,password=unused", subscribe: false, 200, 200);

        await publisher.PublishAsync([new LiveEvent(LiveEventTypes.DashboardChanged, [], true, null)], _ct);
    }

    [Fact]
    public void Deliver_FullConnection_DoesNotBlockOthers()
    {
        var registry = new LiveConnectionRegistry();
        var userId = Guid.NewGuid();
        using var slow = registry.Open(userId, Role.TeamMember);
        using var fast = registry.Open(userId, Role.TeamMember);

        for (var i = 0; i < LiveConnectionRegistry.ConnectionCapacity * 3; i++)
        {
            registry.Deliver(new LiveEvent(LiveEventTypes.DashboardChanged, [userId], false, null));
            fast.Events.TryRead(out _).ShouldBeTrue();
        }

        slow.Events.Count.ShouldBe(LiveConnectionRegistry.ConnectionCapacity);
    }

    [Fact]
    public void Deliver_DisposedConnection_IsRemoved()
    {
        var registry = new LiveConnectionRegistry();
        var userId = Guid.NewGuid();
        var connection = registry.Open(userId, Role.TeamMember);
        connection.Dispose();

        registry.Deliver(new LiveEvent(LiveEventTypes.DashboardChanged, [userId], false, null));

        connection.Events.Completion.IsCompleted.ShouldBeTrue();
        connection.Events.TryRead(out _).ShouldBeFalse();
    }

    private async Task<LiveEvent> ReadAsync(ILiveConnection connection)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(_ct);
        timeout.CancelAfter(ReceiveTimeout);
        return await connection.Events.ReadAsync(timeout.Token);
    }

    private async Task<(RedisLiveEventPublisher Publisher, LiveConnectionRegistry Registry)> CreateAsync(
        string connectionString, bool subscribe = true, int connectTimeoutMs = 2000, int operationTimeoutMs = 1000)
    {
        var redisOptions = new RedisOptions
        {
            ConnectionString = connectionString,
            ConnectTimeoutMs = connectTimeoutMs,
            OperationTimeoutMs = operationTimeoutMs,
        };
        var connection = await ConnectionMultiplexer.ConnectAsync(RedisConnection.BuildConfiguration(redisOptions));
        _connections.Add(connection);
        var options = Options.Create(new NotificationsOptions { RedisChannel = $"notifications-test-{Guid.NewGuid():N}" });
        var registry = new LiveConnectionRegistry();
        var publisher = new RedisLiveEventPublisher(connection, options, NullLogger<RedisLiveEventPublisher>.Instance);
        if (subscribe)
        {
            var subscriber = new RedisLiveEventSubscriber(
                connection, registry, options, TimeProvider.System, NullLogger<RedisLiveEventSubscriber>.Instance);
            _subscribers.Add(subscriber);
            await subscriber.StartAsync(_ct);
            await subscriber.Subscribed;
        }

        return (publisher, registry);
    }
}
