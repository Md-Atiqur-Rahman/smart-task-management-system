using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Dashboard;
using Datavanced.SmartTaskManagement.Application.Features.Dashboard.Queries.GetDashboardSummary;
using Datavanced.SmartTaskManagement.Application.Features.Dashboard.Queries.GetUpcomingTasks;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Dashboard;

public sealed class DashboardQueryHandlerTests
{
    private readonly IDashboardReadRepository _readRepository = Substitute.For<IDashboardReadRepository>();
    private readonly ISecurityContext _security = Substitute.For<ISecurityContext>();

    private void SetCaller(Guid? id, Role? role)
    {
        _security.UserId.Returns(id);
        _security.Role.Returns(role);
    }

    [Fact]
    public async Task Summary_PassesCallerAndUtcToday()
    {
        var callerId = Guid.NewGuid();
        SetCaller(callerId, Role.ProjectManager);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 11, 23, 30, 0, TimeSpan.Zero));
        var dto = new DashboardSummaryDto(
            1, 2, new TaskStatusCountsDto(1, 0, 1, 0), new TaskPriorityCountsDto(0, 2, 0, 0), 1, 1, 0);
        var today = new DateOnly(2026, 11, 11);
        _readRepository.GetSummaryAsync(callerId, Role.ProjectManager, today, Arg.Any<CancellationToken>()).Returns(dto);
        var handler = new GetDashboardSummaryQueryHandler(_security, _readRepository, time);

        var result = await handler.Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        result.Value.ShouldBe(dto);
    }

    [Fact]
    public async Task Upcoming_PassesOptionsDaysAndLimit()
    {
        var callerId = Guid.NewGuid();
        SetCaller(callerId, Role.TeamMember);
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 11, 0, 0, 0, TimeSpan.Zero));
        var options = Options.Create(new DashboardOptions { UpcomingDays = 7, UpcomingLimit = 10 });
        IReadOnlyList<UpcomingTaskDto> items = [];
        _readRepository.GetUpcomingAsync(
            callerId, Role.TeamMember, new DateOnly(2026, 11, 11), 7, 10, Arg.Any<CancellationToken>()).Returns(items);
        var handler = new GetUpcomingTasksQueryHandler(_security, _readRepository, options, time);

        var result = await handler.Handle(new GetUpcomingTasksQuery(), CancellationToken.None);

        result.Value.ShouldBeSameAs(items);
    }

    [Fact]
    public async Task Summary_NoRoleInSecurityContext_ReturnsTokenRevoked()
    {
        SetCaller(Guid.NewGuid(), null);
        var handler = new GetDashboardSummaryQueryHandler(_security, _readRepository, new FakeTimeProvider());

        var result = await handler.Handle(new GetDashboardSummaryQuery(), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.TokenRevoked);
    }

    [Fact]
    public async Task Upcoming_NoUserIdInSecurityContext_ReturnsTokenRevoked()
    {
        SetCaller(null, Role.Admin);
        var handler = new GetUpcomingTasksQueryHandler(
            _security, _readRepository, Options.Create(new DashboardOptions()), new FakeTimeProvider());

        var result = await handler.Handle(new GetUpcomingTasksQuery(), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.TokenRevoked);
    }
}
