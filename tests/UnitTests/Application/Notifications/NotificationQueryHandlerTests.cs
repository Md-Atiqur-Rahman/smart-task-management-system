using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Application.Features.Notifications.Queries.GetUnreadCount;
using Datavanced.SmartTaskManagement.Application.Features.Notifications.Queries.ListNotifications;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Notifications;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Notifications;

public sealed class NotificationQueryHandlerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 11, 12, 0, 0, TimeSpan.Zero);

    private readonly INotificationReadRepository _readRepository = Substitute.For<INotificationReadRepository>();
    private readonly ISecurityContext _security = Substitute.For<ISecurityContext>();
    private readonly FakeTimeProvider _time = new(Now);
    private readonly IOptions<NotificationsOptions> _options =
        Options.Create(new NotificationsOptions { RetentionDays = 7, ListLimit = 50 });

    [Fact]
    public async Task List_PassesWindowAndLimitAndRendersMessage()
    {
        var callerId = Guid.NewGuid();
        _security.UserId.Returns(callerId);
        _security.Role.Returns(Role.TeamMember);
        var row = new NotificationRow(
            Guid.NewGuid(), NotificationType.TaskAssigned, "Docs", "Ann", Guid.NewGuid(), null, Now, null);
        _readRepository.ListAsync(callerId, Now.AddDays(-7), 50, Arg.Any<CancellationToken>())
            .Returns([row]);
        var handler = new ListNotificationsQueryHandler(_security, _readRepository, _options, _time);

        var result = await handler.Handle(new ListNotificationsQuery(), CancellationToken.None);

        var dto = result.Value.ShouldHaveSingleItem();
        dto.Message.ShouldBe("Ann assigned you the task \"Docs\".");
        dto.IsRead.ShouldBeFalse();
        dto.TaskId.ShouldBe(row.TaskId);
    }

    [Fact]
    public async Task UnreadCount_PassesWindow()
    {
        var callerId = Guid.NewGuid();
        _security.UserId.Returns(callerId);
        _security.Role.Returns(Role.TeamMember);
        _readRepository.CountUnreadAsync(callerId, Now.AddDays(-7), Arg.Any<CancellationToken>()).Returns(3);
        var handler = new GetUnreadCountQueryHandler(_security, _readRepository, _options, _time);

        var result = await handler.Handle(new GetUnreadCountQuery(), CancellationToken.None);

        result.Value.Count.ShouldBe(3);
    }

    [Fact]
    public async Task List_WithoutIdentity_ReturnsTokenRevoked()
    {
        _security.UserId.Returns((Guid?)null);
        _security.Role.Returns((Role?)null);
        var handler = new ListNotificationsQueryHandler(_security, _readRepository, _options, _time);

        var result = await handler.Handle(new ListNotificationsQuery(), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.TokenRevoked);
    }
}
