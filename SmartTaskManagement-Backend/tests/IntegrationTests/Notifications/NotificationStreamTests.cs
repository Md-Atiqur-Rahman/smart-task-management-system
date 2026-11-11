using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Net.ServerSentEvents;
using System.Text.Json;

using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.LiveEvents;
using Datavanced.SmartTaskManagement.IntegrationTests.Api;
using Datavanced.SmartTaskManagement.IntegrationTests.Auth;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Time.Testing;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Notifications;

/// <summary>
/// The live stream (GET /api/notifications/stream) through the real host. Every wait is bounded:
/// a missing event or a stream that does not end fails the test instead of hanging it.
/// </summary>
[Collection(AuthCollectionDefinition.Name)]
public sealed class NotificationStreamTests(SqlServerFixture database, RedisFixture redis) : IDisposable
{
    private const string StreamPath = "/api/notifications/stream";
    private const string ProjectsPath = "/api/projects";
    private const string TasksPath = "/api/tasks";
    private const string HeartbeatSeconds = "5";

    // Longer than one heartbeat (5 s) plus the time a command needs; short enough to fail fast.
    private static readonly TimeSpan StreamTimeout = TimeSpan.FromSeconds(30);

    private readonly List<AuthApi> _apis = [];
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public void Dispose()
    {
        foreach (var api in _apis)
        {
            api.Dispose();
        }
    }

    [Fact]
    public async Task Stream_ReceivesNotificationWhenTaskAssigned()
    {
        var api = CreateApi();
        var pm = await api.CreateUserAsync(Role.ProjectManager, _ct, "pm");
        var member = await api.CreateUserAsync(Role.TeamMember, _ct);
        var taskId = await CreateTaskAsync(api, pm, "Write docs");
        await using var stream = await LiveStream.OpenAsync(api, member, StreamTimeout, _ct);
        stream.ContentType.ShouldBe("text/event-stream");
        await stream.ReadReadyAsync();

        await AssignAsync(api, pm, taskId, member.Id);

        var item = await stream.ReadUntilAsync(LiveEventTypes.Notification);
        var data = JsonDocument.Parse(item.Data).RootElement;
        data.GetProperty("message").GetString().ShouldBe("User pm assigned you the task \"Write docs\".");
        // The API's JSON convention: camelCase and enums as names (the Redis message uses numbers).
        data.GetProperty("type").GetString().ShouldBe("TaskAssigned");
        data.GetProperty("taskId").GetGuid().ShouldBe(taskId);
        data.GetProperty("isRead").GetBoolean().ShouldBeFalse();
    }

    [Fact]
    public async Task Stream_ReceivesDashboardChangedOnStatusChange()
    {
        var api = CreateApi();
        var pm = await api.CreateUserAsync(Role.ProjectManager, _ct);
        var member = await api.CreateUserAsync(Role.TeamMember, _ct);
        var taskId = await CreateTaskAsync(api, pm, "Status task");
        await AssignAsync(api, pm, taskId, member.Id);
        await using var stream = await LiveStream.OpenAsync(api, pm, StreamTimeout, _ct);
        await stream.ReadReadyAsync();

        using var status = await api.SendAsync(
            HttpMethod.Put, $"{TasksPath}/{taskId}/status", member.Token, new { status = "InProgress" }, _ct);
        status.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var item = await stream.ReadUntilAsync(LiveEventTypes.DashboardChanged);
        item.Data.ShouldBeEmpty();
    }

    // Review Focus 5 (ruling 2): a role change bumps the token version; logout does not (PRD 5).
    [Fact]
    public async Task Stream_EndsAfterRoleChange()
    {
        var api = CreateApi();
        var admin = await api.CreateUserAsync(Role.Admin, _ct);
        var member = await api.CreateUserAsync(Role.TeamMember, _ct);
        await using var stream = await LiveStream.OpenAsync(api, member, StreamTimeout, _ct);
        await stream.ReadReadyAsync();

        using var change = await api.SendAsync(
            HttpMethod.Put, $"/api/users/{member.Id}/role", admin.Token, new { role = nameof(Role.ProjectManager) }, _ct);
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        await stream.ReadToEndAsync();
    }

    [Fact]
    public async Task Stream_EndsWhenTokenExpires()
    {
        var clock = new FakeTimeProvider(DateTimeOffset.UtcNow);
        var api = CreateApi(factory => factory
            .WithTimeProvider(clock)
            .WithSetting("Jwt:AccessTokenMinutes", "5"));
        var member = await api.CreateUserAsync(Role.TeamMember, _ct);
        await using var stream = await LiveStream.OpenAsync(api, member, StreamTimeout, _ct);
        await stream.ReadReadyAsync();

        clock.Advance(TimeSpan.FromMinutes(5) + TimeSpan.FromSeconds(1));

        // The server may arm its next timer on the fake clock just after the advance; keep the clock
        // moving (by less than the heartbeat) until the stream ends. The stream's timeout bounds the loop.
        var end = stream.ReadToEndAsync();
        while (!end.IsCompleted)
        {
            clock.Advance(TimeSpan.FromSeconds(1));
            await Task.WhenAny(end, Task.Delay(TimeSpan.FromMilliseconds(50), _ct));
        }

        await end;
    }

    [Fact]
    public async Task Stream_NoToken_Returns401()
    {
        var api = CreateApi();
        using var request = new HttpRequestMessage(HttpMethod.Get, AuthApi.Uri(StreamPath));

        using var response = await api.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Stream_ThroughRedis_ReachesUserAcrossFactories()
    {
        var channel = $"notifications-test-{Guid.NewGuid():N}";
        var writer = CreateApi(f => f.WithSetting("Notifications:RedisChannel", channel), redis.ConnectionString);
        var reader = CreateApi(f => f.WithSetting("Notifications:RedisChannel", channel), redis.ConnectionString);
        await SubscribedAsync(writer);
        await SubscribedAsync(reader);
        var pm = await writer.CreateUserAsync(Role.ProjectManager, _ct, "pm");
        var member = await writer.CreateUserAsync(Role.TeamMember, _ct);
        var taskId = await CreateTaskAsync(writer, pm, "Across instances");
        await using var stream = await LiveStream.OpenAsync(reader, member, StreamTimeout, _ct);
        await stream.ReadReadyAsync();

        await AssignAsync(writer, pm, taskId, member.Id);

        var item = await stream.ReadUntilAsync(LiveEventTypes.Notification);
        var data = JsonDocument.Parse(item.Data).RootElement;
        data.GetProperty("type").GetString().ShouldBe("TaskAssigned");
        data.GetProperty("taskId").GetGuid().ShouldBe(taskId);
        data.GetProperty("message").GetString().ShouldBe("User pm assigned you the task \"Across instances\".");
    }

    private AuthApi CreateApi(Action<ApiFactory>? configure = null, string? redisConnectionString = null)
    {
        var api = new AuthApi(database, redisConnectionString, factory =>
        {
            factory.WithSetting("Notifications:HeartbeatSeconds", HeartbeatSeconds);
            configure?.Invoke(factory);
        });
        _apis.Add(api);
        return api;
    }

    private async Task SubscribedAsync(AuthApi api)
    {
        var subscriber = api.Factory.Services.GetServices<IHostedService>().OfType<RedisLiveEventSubscriber>().Single();
        await subscriber.Subscribed.WaitAsync(StreamTimeout, _ct);
    }

    private async Task AssignAsync(AuthApi api, TestUser caller, Guid taskId, Guid assigneeId)
    {
        using var response = await api.SendAsync(
            HttpMethod.Put, $"{TasksPath}/{taskId}/assignee", caller.Token, new { assigneeId }, _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<Guid> CreateTaskAsync(AuthApi api, TestUser caller, string title)
    {
        using var project = await api.SendAsync(
            HttpMethod.Post, ProjectsPath, caller.Token, new { name = "Stream project" }, _ct);
        project.StatusCode.ShouldBe(HttpStatusCode.Created);
        var projectId = (await project.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("id").GetGuid();

        using var task = await api.SendAsync(
            HttpMethod.Post, $"{ProjectsPath}/{projectId}/tasks", caller.Token, new { title }, _ct);
        task.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await task.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("id").GetGuid();
    }

    /// <summary>An open stream read with the SSE parser; every read is bounded by one timeout.</summary>
    private sealed class LiveStream : IAsyncDisposable
    {
        private readonly HttpResponseMessage _response;
        private readonly CancellationTokenSource _timeout;
        private readonly IAsyncEnumerator<SseItem<string>> _items;

        private LiveStream(HttpResponseMessage response, CancellationTokenSource timeout, Stream body)
        {
            _response = response;
            _timeout = timeout;
            _items = SseParser.Create(body).EnumerateAsync(timeout.Token).GetAsyncEnumerator(timeout.Token);
        }

        public string? ContentType => _response.Content.Headers.ContentType?.MediaType;

        public static async Task<LiveStream> OpenAsync(AuthApi api, TestUser user, TimeSpan timeout, CancellationToken ct)
        {
            var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(timeout);
            using var request = new HttpRequestMessage(HttpMethod.Get, AuthApi.Uri(StreamPath));
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", user.Token);
            var response = await api.Client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cts.Token);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var body = await response.Content.ReadAsStreamAsync(cts.Token);
            return new LiveStream(response, cts, body);
        }

        /// <summary>The first item is a heartbeat sent once the connection is registered.</summary>
        public async Task ReadReadyAsync()
        {
            var item = await NextAsync();
            item.ShouldNotBeNull("The stream ended before it was ready.");
            item.Value.EventType.ShouldBe(LiveEventTypes.Heartbeat);
        }

        public async Task<SseItem<string>> ReadUntilAsync(string eventType)
        {
            while (true)
            {
                var item = await NextAsync();
                item.ShouldNotBeNull($"The stream ended before a '{eventType}' event.");
                if (item.Value.EventType == eventType)
                {
                    return item.Value;
                }
            }
        }

        /// <summary>Reads until the server ends the stream; the timeout fails the test if it never does.</summary>
        public async Task ReadToEndAsync()
        {
            while (await NextAsync() is not null)
            {
            }
        }

        public async ValueTask DisposeAsync()
        {
            await _items.DisposeAsync();
            _response.Dispose();
            _timeout.Dispose();
        }

        private async Task<SseItem<string>?> NextAsync() =>
            await _items.MoveNextAsync() ? _items.Current : null;
    }
}
