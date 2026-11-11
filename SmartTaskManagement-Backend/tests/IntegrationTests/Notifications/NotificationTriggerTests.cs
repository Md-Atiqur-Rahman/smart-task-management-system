using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;

using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.AssignTask;
using Datavanced.SmartTaskManagement.Domain.Notifications;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.Infrastructure.LiveEvents;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;
using Datavanced.SmartTaskManagement.IntegrationTests.Auth;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Notifications;

/// <summary>
/// The task and project commands store notifications in the same save (real host, real SQL Server).
/// The host has no Redis: live events go to the in-process publisher, so tests can see what was
/// published. The database is shared: every test asserts only on the fresh users it creates.
/// </summary>
[Collection(AuthCollectionDefinition.Name)]
public sealed class NotificationTriggerTests(SqlServerFixture database) : IDisposable
{
    private const string ProjectsPath = "/api/projects";
    private const string TasksPath = "/api/tasks";

    private readonly AuthApi _api = new(database, redisConnectionString: null);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private InProcessLiveEventPublisher Publisher =>
        _api.Factory.Services.GetRequiredService<InProcessLiveEventPublisher>();

    [Fact]
    public async Task AssignTask_StoresNotificationForAssignee()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct, "pm");
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var projectId = await CreateProjectAsync(_api, pm, new { name = "Notify project" });
        var taskId = await CreateTaskAsync(_api, pm, projectId, new { title = "Write docs" });

        using var assign = await AssignAsync(_api, pm, taskId, member.Id);
        assign.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var notification = (await NotificationsOfAsync(member.Id)).ShouldHaveSingleItem();
        notification.Type.ShouldBe(NotificationType.TaskAssigned);
        notification.TaskId.ShouldBe(taskId);
        notification.Subject.ShouldBe("Write docs");
        notification.ActorName.ShouldBe("User pm");
        notification.ReadAt.ShouldBeNull();
        notification.EmailStatus.ShouldBe(EmailDeliveryStatus.Pending);
        (await NotificationsOfAsync(pm.Id)).ShouldBeEmpty();

        var live = Publisher.Published
            .Where(e => e.Type == LiveEventTypes.Notification && e.Notification?.Id == notification.Id)
            .ShouldHaveSingleItem();
        live.UserIds.ShouldBe(new[] { member.Id });
        live.Notification.ShouldNotBeNull().Message.ShouldBe("User pm assigned you the task \"Write docs\".");
        Publisher.Published.ShouldContain(e => e.Type == LiveEventTypes.DashboardChanged
            && e.IncludeAdmins && e.UserIds.Contains(member.Id) && e.UserIds.Contains(pm.Id));
    }

    // Review Focus 2: assigning the same user again changes nothing and creates nothing.
    [Fact]
    public async Task AssignSameUserAgain_StoresNothingNew()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var projectId = await CreateProjectAsync(_api, pm, new { name = "Notify project" });
        var taskId = await CreateTaskAsync(_api, pm, projectId, new { title = "Once" });

        using (var first = await AssignAsync(_api, pm, taskId, member.Id))
        {
            first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        var eventsForMember = Publisher.Published.Count(e => e.UserIds.Contains(member.Id));

        using var second = await AssignAsync(_api, pm, taskId, member.Id);
        second.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        (await NotificationsOfAsync(member.Id)).ShouldHaveSingleItem();
        Publisher.Published.Count(e => e.UserIds.Contains(member.Id)).ShouldBe(eventsForMember);
    }

    [Fact]
    public async Task CreateTaskWithAssignee_StoresNotification()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var projectId = await CreateProjectAsync(_api, pm, new { name = "Notify project" });

        var taskId = await CreateTaskAsync(_api, pm, projectId, new { title = "Born assigned", assigneeId = member.Id });

        var notification = (await NotificationsOfAsync(member.Id)).ShouldHaveSingleItem();
        notification.Type.ShouldBe(NotificationType.TaskAssigned);
        notification.TaskId.ShouldBe(taskId);
        notification.Subject.ShouldBe("Born assigned");
    }

    [Fact]
    public async Task ChangeProjectOwner_StoresTwoNotifications()
    {
        var admin = await _api.CreateUserAsync(Role.Admin, _ct, "admin");
        var firstPm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var secondPm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var projectId = await CreateProjectAsync(_api, admin, new { name = "Handover", ownerId = firstPm.Id });
        var createdFor = (await NotificationsOfAsync(firstPm.Id)).ShouldHaveSingleItem();
        createdFor.Type.ShouldBe(NotificationType.ProjectOwnerAssigned);

        using var change = await _api.SendAsync(
            HttpMethod.Put, $"{ProjectsPath}/{projectId}/owner", admin.Token, new { ownerId = secondPm.Id }, _ct);
        change.StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var assigned = (await NotificationsOfAsync(secondPm.Id)).ShouldHaveSingleItem();
        assigned.Type.ShouldBe(NotificationType.ProjectOwnerAssigned);
        assigned.ProjectId.ShouldBe(projectId);
        assigned.Subject.ShouldBe("Handover");
        assigned.ActorName.ShouldBe("User admin");
        var removed = (await NotificationsOfAsync(firstPm.Id)).Where(n => n.Id != createdFor.Id).ShouldHaveSingleItem();
        removed.Type.ShouldBe(NotificationType.ProjectOwnerRemoved);
        removed.ProjectId.ShouldBeNull();
        removed.Subject.ShouldBe("Handover");
        (await NotificationsOfAsync(admin.Id)).ShouldBeEmpty();
    }

    // Review Focus 3: a stale RowVersion fails the save; nothing is stored and nothing is published.
    [Fact]
    public async Task FailedSave_StoresNoNotificationAndPublishesNothing()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);
        var projectId = await CreateProjectAsync(_api, pm, new { name = "Notify project" });
        var taskId = await CreateTaskAsync(_api, pm, projectId, new { title = "Contended" });

        using var scope = _api.Factory.Services.CreateScope();
        // Track the current row (and its RowVersion) in this scope; the handler then works on that copy.
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.Set<TaskItem>().SingleAsync(t => t.Id == taskId, _ct)).ShouldNotBeNull();

        // Another request changes the row first.
        using (var update = await _api.SendAsync(
            HttpMethod.Put, $"{TasksPath}/{taskId}", pm.Token, new { title = "Changed elsewhere", priority = "High" }, _ct))
        {
            update.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        }

        scope.ServiceProvider.GetRequiredService<ISecurityContextSetter>().SetPrincipal(new ClaimsPrincipal(
            new ClaimsIdentity(
                [
                    new Claim(SecurityClaimTypes.UserId, pm.Id.ToString()),
                    new Claim(SecurityClaimTypes.Role, nameof(Role.ProjectManager)),
                ],
                "Test")));
        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<AssignTaskCommand>>();

        var result = await handler.Handle(new AssignTaskCommand(taskId, member.Id), _ct);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe(UnitOfWork.ConcurrencyConflictCode);
        (await NotificationsOfAsync(member.Id)).ShouldBeEmpty();
        Publisher.Published.ShouldNotContain(e => e.UserIds.Contains(member.Id));
    }

    // Review Focus 5: only the live event publisher points at an unreachable Redis (the token version
    // store stays in memory, because with Redis fully down every authenticated request is 503, PRD 6).
    [Fact]
    public async Task AssignTask_RedisDown_StillSucceeds()
    {
        using var api = new AuthApi(database, redisConnectionString: null, factory => factory
            .WithSetting("Redis:ConnectionString", "127.0.0.1:1,password=unused")
            .WithSetting("Redis:ConnectTimeoutMs", "200")
            .WithSetting("Redis:OperationTimeoutMs", "200")
            .WithTestServices(services =>
            {
                services.RemoveAll<ILiveEventPublisher>();
                services.AddSingleton<ILiveEventPublisher, RedisLiveEventPublisher>();
            }));
        api.Factory.Services.GetRequiredService<ILiveEventPublisher>().ShouldBeOfType<RedisLiveEventPublisher>();
        var pm = await api.CreateUserAsync(Role.ProjectManager, _ct);
        var member = await api.CreateUserAsync(Role.TeamMember, _ct);
        var projectId = await CreateProjectAsync(api, pm, new { name = "Redis down" });
        var taskId = await CreateTaskAsync(api, pm, projectId, new { title = "Still works" });

        using var assign = await AssignAsync(api, pm, taskId, member.Id);

        assign.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        (await NotificationsOfAsync(member.Id)).ShouldHaveSingleItem().TaskId.ShouldBe(taskId);
        api.Factory.LogSink.Events.ShouldContain(e =>
            e.MessageTemplate.Text.StartsWith("Live events were not published", StringComparison.Ordinal));
    }

    public void Dispose() => _api.Dispose();

    private async Task<List<Notification>> NotificationsOfAsync(Guid recipientId)
    {
        await using var context = database.CreateDbContext();
        return await context.Set<Notification>().AsNoTracking()
            .Where(n => n.RecipientId == recipientId)
            .OrderBy(n => n.CreatedAt)
            .ToListAsync(_ct);
    }

    private Task<HttpResponseMessage> AssignAsync(AuthApi api, TestUser caller, Guid taskId, Guid? assigneeId) =>
        api.SendAsync(HttpMethod.Put, $"{TasksPath}/{taskId}/assignee", caller.Token, new { assigneeId }, _ct);

    private async Task<Guid> CreateProjectAsync(AuthApi api, TestUser caller, object body)
    {
        using var response = await api.SendAsync(HttpMethod.Post, ProjectsPath, caller.Token, body, _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("id").GetGuid();
    }

    private async Task<Guid> CreateTaskAsync(AuthApi api, TestUser caller, Guid projectId, object body)
    {
        using var response = await api.SendAsync(
            HttpMethod.Post, $"{ProjectsPath}/{projectId}/tasks", caller.Token, body, _ct);
        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        return (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("id").GetGuid();
    }
}
