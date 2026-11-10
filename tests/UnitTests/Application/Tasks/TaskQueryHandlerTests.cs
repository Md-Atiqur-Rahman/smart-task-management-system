using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Tasks;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries.GetTaskById;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries.ListProjectTasks;
using Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries.ListTasks;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.Extensions.Options;

using NSubstitute;

using SortDirection = Datavanced.SmartTaskManagement.Application.Abstractions.Paging.SortDirection;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Tasks;

public sealed class TaskQueryHandlerTests
{
    private readonly ITaskItemReadRepository _readRepository = Substitute.For<ITaskItemReadRepository>();
    private readonly ISecurityContext _security = Substitute.For<ISecurityContext>();

    private static ListTasksQuery NewListQuery(
        Guid? assigneeId = null,
        bool unassigned = false,
        DateOnly? dueFrom = null,
        DateOnly? dueTo = null,
        string? sortBy = null,
        Guid? projectId = null) =>
        new(null, null, null, assigneeId, unassigned, dueFrom, dueTo, sortBy, SortDirection.Ascending, 1, 20, projectId);

    private static ListTasksQueryValidator NewValidator() =>
        new(Options.Create(new PagingOptions { DefaultPageSize = 20, MaxPageSize = 100 }));

    private static ListProjectTasksQueryValidator NewProjectValidator() =>
        new(Options.Create(new PagingOptions { DefaultPageSize = 20, MaxPageSize = 100 }));

    private void SetCaller(Guid? id, Role? role)
    {
        _security.UserId.Returns(id);
        _security.Role.Returns(role);
    }

    [Fact]
    public async Task GetById_NotVisible_ReturnsNotFound()
    {
        var callerId = Guid.NewGuid();
        var taskId = Guid.NewGuid();
        SetCaller(callerId, Role.TeamMember);
        var handler = new GetTaskByIdQueryHandler(_security, _readRepository);

        var result = await handler.Handle(new GetTaskByIdQuery(taskId), CancellationToken.None);

        result.Error.ShouldBe(TaskErrors.NotFound);
        await _readRepository.Received(1).GetByIdAsync(taskId, callerId, Role.TeamMember, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task GetById_Visible_ReturnsDto()
    {
        var callerId = Guid.NewGuid();
        SetCaller(callerId, Role.Admin);
        var dto = new TaskDetailsDto(
            Guid.NewGuid(), "T", null, TaskItemStatus.ToDo, TaskPriority.Low, null, null,
            new TaskProjectDto(Guid.NewGuid(), "P"), DateTimeOffset.UnixEpoch, null);
        _readRepository.GetByIdAsync(dto.Id, callerId, Role.Admin, Arg.Any<CancellationToken>()).Returns(dto);
        var handler = new GetTaskByIdQueryHandler(_security, _readRepository);

        var result = await handler.Handle(new GetTaskByIdQuery(dto.Id), CancellationToken.None);

        result.Value.ShouldBe(dto);
    }

    [Fact]
    public async Task GetById_NoUserInSecurityContext_ReturnsTokenRevoked()
    {
        SetCaller(null, Role.Admin);
        var handler = new GetTaskByIdQueryHandler(_security, _readRepository);

        var result = await handler.Handle(new GetTaskByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.TokenRevoked);
        await _readRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Role>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListProjectTasks_ProjectNotVisible_ReturnsProjectNotFound()
    {
        var callerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        SetCaller(callerId, Role.TeamMember);
        _readRepository.IsProjectVisibleAsync(projectId, callerId, Role.TeamMember, Arg.Any<CancellationToken>())
            .Returns(false);
        var handler = new ListProjectTasksQueryHandler(_security, _readRepository);
        var query = new ListProjectTasksQuery(
            projectId, null, null, null, null, false, null, null, null, SortDirection.Ascending, 1, 20);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Error.ShouldBe(ProjectErrors.NotFound);
        await _readRepository.DidNotReceive().ListAsync(
            Arg.Any<TaskListQuery>(), Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<Role>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListProjectTasks_ProjectVisible_PassesRouteProjectToRepository()
    {
        var callerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        SetCaller(callerId, Role.ProjectManager);
        var query = new ListProjectTasksQuery(
            projectId, null, null, null, null, false, null, null, null, SortDirection.Ascending, 1, 20);
        var page = new PagedResult<TaskListItemDto>([], 1, 20, 0);
        _readRepository.IsProjectVisibleAsync(projectId, callerId, Role.ProjectManager, Arg.Any<CancellationToken>())
            .Returns(true);
        _readRepository.ListAsync(query, projectId, callerId, Role.ProjectManager, Arg.Any<CancellationToken>())
            .Returns(page);
        var handler = new ListProjectTasksQueryHandler(_security, _readRepository);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Value.ShouldBe(page);
    }

    [Fact]
    public async Task ListTasks_PassesCallerAndNullProjectToRepository()
    {
        var callerId = Guid.NewGuid();
        SetCaller(callerId, Role.TeamMember);
        var query = NewListQuery();
        var page = new PagedResult<TaskListItemDto>([], 1, 20, 0);
        _readRepository.ListAsync(query, null, callerId, Role.TeamMember, Arg.Any<CancellationToken>()).Returns(page);
        var handler = new ListTasksQueryHandler(_security, _readRepository);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Value.ShouldBe(page);
        await _readRepository.Received(1).ListAsync(query, null, callerId, Role.TeamMember, Arg.Any<CancellationToken>());
        await _readRepository.DidNotReceive().IsProjectVisibleAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Role>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListTasks_ProjectFilter_PassesItToRepositoryWithoutVisibilityCheck()
    {
        var callerId = Guid.NewGuid();
        var projectId = Guid.NewGuid();
        SetCaller(callerId, Role.TeamMember);
        var query = NewListQuery(projectId: projectId);
        var page = new PagedResult<TaskListItemDto>([], 1, 20, 0);
        _readRepository.ListAsync(query, projectId, callerId, Role.TeamMember, Arg.Any<CancellationToken>())
            .Returns(page);
        var handler = new ListTasksQueryHandler(_security, _readRepository);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Value.ShouldBe(page);
        await _readRepository.DidNotReceive().IsProjectVisibleAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Role>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ListTasks_NoRoleInSecurityContext_ReturnsTokenRevoked()
    {
        SetCaller(Guid.NewGuid(), null);
        var handler = new ListTasksQueryHandler(_security, _readRepository);

        var result = await handler.Handle(NewListQuery(), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.TokenRevoked);
        await _readRepository.DidNotReceive().ListAsync(
            Arg.Any<TaskListQuery>(), Arg.Any<Guid?>(), Arg.Any<Guid>(), Arg.Any<Role>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public void Validator_AssigneeIdWithUnassigned_Fails()
    {
        var result = NewValidator().Validate(NewListQuery(assigneeId: Guid.NewGuid(), unassigned: true));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(TaskListQuery.AssigneeId));
        NewValidator().Validate(NewListQuery(assigneeId: Guid.NewGuid())).IsValid.ShouldBeTrue();
        NewValidator().Validate(NewListQuery(unassigned: true)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validator_DueFromAfterDueTo_Fails()
    {
        var result = NewValidator().Validate(
            NewListQuery(dueFrom: new DateOnly(2026, 11, 20), dueTo: new DateOnly(2026, 11, 19)));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(TaskListQuery.DueFrom));
        NewValidator().Validate(
            NewListQuery(dueFrom: new DateOnly(2026, 11, 20), dueTo: new DateOnly(2026, 11, 20))).IsValid.ShouldBeTrue();
        NewValidator().Validate(NewListQuery(dueFrom: new DateOnly(2026, 11, 20))).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void Validator_UndefinedEnums_Fail()
    {
        var query = NewListQuery() with { Status = (TaskItemStatus)42, Priority = (TaskPriority)42 };

        var result = NewValidator().Validate(query);

        result.Errors.ShouldContain(e => e.PropertyName == nameof(TaskListQuery.Status));
        result.Errors.ShouldContain(e => e.PropertyName == nameof(TaskListQuery.Priority));
    }

    [Fact]
    public void Validator_SortByPriority_Passes_SortByPassword_Fails()
    {
        NewValidator().Validate(NewListQuery(sortBy: TaskSortFields.Priority)).IsValid.ShouldBeTrue();
        NewValidator().Validate(NewListQuery(sortBy: "password"))
            .Errors.ShouldContain(e => e.PropertyName == nameof(ListQuery.SortBy));

        var projectQuery = new ListProjectTasksQuery(
            Guid.NewGuid(), null, null, null, null, false, null, null, "password", SortDirection.Ascending, 1, 20);
        NewProjectValidator().Validate(projectQuery)
            .Errors.ShouldContain(e => e.PropertyName == nameof(ListQuery.SortBy));
    }

    [Fact]
    public void ProjectValidator_EmptyProjectId_Fails()
    {
        var query = new ListProjectTasksQuery(
            Guid.Empty, null, null, null, null, false, null, null, null, SortDirection.Ascending, 1, 20);

        NewProjectValidator().Validate(query)
            .Errors.ShouldContain(e => e.PropertyName == nameof(ListProjectTasksQuery.ProjectId));
    }
}
