using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.Domain.Notifications;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.IntegrationTests.Auth;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Notifications;

/// <summary>Notification endpoints through the real host. Each test uses fresh users only (shared database).</summary>
[Collection(AuthCollectionDefinition.Name)]
public sealed class NotificationEndpointsTests(SqlServerFixture database) : IDisposable
{
    private const string NotificationsPath = "/api/notifications";
    private const string ProjectsPath = "/api/projects";
    private const string TasksPath = "/api/tasks";

    private readonly AuthApi _api = new(database, redisConnectionString: null);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public void Dispose() => _api.Dispose();

    [Fact]
    public async Task List_ReturnsOnlyOwnNewestFirstWithMessage()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct, "pm");
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var other = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var projectId = await CreateProjectAsync(pm);
        var first = await CreateTaskAsync(pm, projectId, "First");
        var second = await CreateTaskAsync(pm, projectId, "Second");
        var third = await CreateTaskAsync(pm, projectId, "Third");
        await AssignAsync(pm, first, member.Id);
        await AssignAsync(pm, second, member.Id);
        await AssignAsync(pm, third, other.Id);

        using var response = await _api.SendAsync(HttpMethod.Get, NotificationsPath, member.Token, null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var items = (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).EnumerateArray().ToList();
        items.Count.ShouldBe(2);
        items[0].GetProperty("taskId").GetGuid().ShouldBe(second);
        items[1].GetProperty("taskId").GetGuid().ShouldBe(first);
        items[0].GetProperty("message").GetString().ShouldBe("User pm assigned you the task \"Second\".");
        items[0].GetProperty("type").GetString().ShouldBe("TaskAssigned");
        items[0].GetProperty("isRead").GetBoolean().ShouldBeFalse();
        items[0].GetProperty("projectId").ValueKind.ShouldBe(JsonValueKind.Null);
    }

    [Fact]
    public async Task List_ExcludesOlderThanRetention()
    {
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        await InsertAsync(member.Id, DateTimeOffset.UtcNow.AddDays(-8));
        await InsertAsync(member.Id, DateTimeOffset.UtcNow.AddMinutes(-5));

        using var list = await _api.SendAsync(HttpMethod.Get, NotificationsPath, member.Token, null, _ct);

        (await list.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetArrayLength().ShouldBe(1);
        (await UnreadCountAsync(member)).ShouldBe(1);
    }

    [Fact]
    public async Task UnreadCount_CountsOnlyOwnUnreadInWindow()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var other = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var projectId = await CreateProjectAsync(pm);
        await AssignAsync(pm, await CreateTaskAsync(pm, projectId, "A"), member.Id);
        await AssignAsync(pm, await CreateTaskAsync(pm, projectId, "B"), member.Id);
        await AssignAsync(pm, await CreateTaskAsync(pm, projectId, "C"), other.Id);
        await InsertAsync(member.Id, DateTimeOffset.UtcNow.AddDays(-8));

        (await UnreadCountAsync(member)).ShouldBe(2);
        (await UnreadCountAsync(other)).ShouldBe(1);
    }

    // Review Focus 4.
    [Fact]
    public async Task MarkRead_OtherUsersNotification_Returns404()
    {
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var intruder = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var id = await InsertAsync(member.Id, DateTimeOffset.UtcNow);

        using var response = await _api.SendAsync(
            HttpMethod.Post, $"{NotificationsPath}/{id}/read", intruder.Token, null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errorCode").GetString()
            .ShouldBe("notifications.not_found");
        (await UnreadCountAsync(member)).ShouldBe(1);
    }

    [Fact]
    public async Task MarkRead_OlderThanRetention_Returns404()
    {
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var id = await InsertAsync(member.Id, DateTimeOffset.UtcNow.AddDays(-8));

        using var response = await _api.SendAsync(
            HttpMethod.Post, $"{NotificationsPath}/{id}/read", member.Token, null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task MarkRead_ThenCount_DropsByOne()
    {
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var id = await InsertAsync(member.Id, DateTimeOffset.UtcNow.AddMinutes(-2));
        await InsertAsync(member.Id, DateTimeOffset.UtcNow.AddMinutes(-1));
        (await UnreadCountAsync(member)).ShouldBe(2);

        using var response = await _api.SendAsync(
            HttpMethod.Post, $"{NotificationsPath}/{id}/read", member.Token, null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await UnreadCountAsync(member)).ShouldBe(1);

        using var again = await _api.SendAsync(
            HttpMethod.Post, $"{NotificationsPath}/{id}/read", member.Token, null, _ct);
        again.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await UnreadCountAsync(member)).ShouldBe(1);
    }

    [Fact]
    public async Task ReadAll_MarksEveryOwnUnread()
    {
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var other = await _api.CreateUserAsync(Role.TeamMember, _ct);
        await InsertAsync(member.Id, DateTimeOffset.UtcNow.AddMinutes(-2));
        await InsertAsync(member.Id, DateTimeOffset.UtcNow.AddMinutes(-1));
        await InsertAsync(other.Id, DateTimeOffset.UtcNow);

        using var response = await _api.SendAsync(
            HttpMethod.Post, $"{NotificationsPath}/read-all", member.Token, null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await UnreadCountAsync(member)).ShouldBe(0);
        (await UnreadCountAsync(other)).ShouldBe(1);

        using var list = await _api.SendAsync(HttpMethod.Get, NotificationsPath, member.Token, null, _ct);
        var items = (await list.Content.ReadFromJsonAsync<JsonElement>(_ct)).EnumerateArray().ToList();
        items.Count.ShouldBe(2);
        items.ShouldAllBe(n => n.GetProperty("isRead").GetBoolean());
    }

    [Fact]
    public async Task Endpoints_NoToken_Return401()
    {
        var id = Guid.NewGuid();
        foreach (var (method, path) in new[]
        {
            (HttpMethod.Get, NotificationsPath),
            (HttpMethod.Get, $"{NotificationsPath}/unread-count"),
            (HttpMethod.Post, $"{NotificationsPath}/{id}/read"),
            (HttpMethod.Post, $"{NotificationsPath}/read-all"),
        })
        {
            using var request = new HttpRequestMessage(method, AuthApi.Uri(path));
            using var response = await _api.Client.SendAsync(request, _ct);
            response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        }
    }

    private async Task<int> UnreadCountAsync(TestUser user)
    {
        using var response = await _api.SendAsync(
            HttpMethod.Get, $"{NotificationsPath}/unread-count", user.Token, null, _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("count").GetInt32();
    }

    private async Task<Guid> InsertAsync(Guid recipientId, DateTimeOffset createdAt)
    {
        var notification = Notification.Create(
            recipientId, NotificationType.TaskUnassigned, null, null, "Old task", "Someone", createdAt);
        await using var context = database.CreateDbContext();
        context.Set<Notification>().Add(notification);
        await context.SaveChangesAsync(_ct);
        return notification.Id;
    }

    private async Task AssignAsync(TestUser caller, Guid taskId, Guid assigneeId)
    {
        using var response = await _api.SendAsync(
            HttpMethod.Put, $"{TasksPath}/{taskId}/assignee", caller.Token, new { assigneeId }, _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.NoContent);
    }

    private async Task<Guid> CreateProjectAsync(TestUser caller)
    {
        using var response = await _api.SendAsync(
            HttpMethod.Post, ProjectsPath, caller.Token, new { name = "Notify project" }, _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateTaskAsync(TestUser caller, Guid projectId, string title)
    {
        using var response = await _api.SendAsync(
            HttpMethod.Post, $"{ProjectsPath}/{projectId}/tasks", caller.Token, new { title }, _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("id").GetGuid();
    }
}
