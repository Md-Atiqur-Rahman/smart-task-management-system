using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Notifications;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.ChangeProjectOwner;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.CreateProject;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.DeleteProject;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.UpdateProject;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.Extensions.Time.Testing;

using NSubstitute;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Projects;

public sealed class ProjectCommandHandlerTests
{
    private readonly IProjectRepository _projects = Substitute.For<IProjectRepository>();
    private readonly IUserRepository _users = Substitute.For<IUserRepository>();
    private readonly ISecurityContext _security = Substitute.For<ISecurityContext>();
    private readonly IUnitOfWork _unitOfWork = Substitute.For<IUnitOfWork>();
    private readonly FakeTimeProvider _time = new(new DateTimeOffset(2026, 11, 10, 12, 0, 0, TimeSpan.Zero));
    private readonly IAssignmentNotifier _notifier = Substitute.For<IAssignmentNotifier>();
    private readonly LiveEventBuffer _liveEvents = new(Substitute.For<ILiveEventPublisher>());

    public ProjectCommandHandlerTests()
    {
        _unitOfWork.SaveChangesAsync(Arg.Any<CancellationToken>()).Returns(Result.Success());
    }

    private void SetCaller(Guid id, Role role)
    {
        _security.UserId.Returns(id);
        _security.Role.Returns(role);
    }

    private User NewUser(Role role)
    {
        var user = User.Register(Email.Create(Guid.NewGuid().ToString("N") + "@x.com"), "Name", "hash");
        if (role != user.Role)
        {
            user.ChangeRole(role);
        }

        _users.GetByIdAsync(user.Id, Arg.Any<CancellationToken>()).Returns(user);
        return user;
    }

    private Project NewProject(Guid ownerId)
    {
        var project = Project.Create("P", null, ownerId);
        _projects.GetByIdAsync(project.Id, Arg.Any<CancellationToken>()).Returns(project);
        return project;
    }

    private CreateProjectCommandHandler CreateHandler() =>
        new(_security, _projects, _users, _unitOfWork, _notifier, _liveEvents);

    private ChangeProjectOwnerCommandHandler OwnerHandler() =>
        new(_security, _projects, _users, _unitOfWork, _notifier, _liveEvents);

    private DeleteProjectCommandHandler DeleteHandler() =>
        new(_security, _projects, _unitOfWork, _time, _notifier, _liveEvents);

    [Fact]
    public async Task Create_AsAdminWithoutOwner_ReturnsInvalidOwner()
    {
        SetCaller(Guid.NewGuid(), Role.Admin);

        var result = await CreateHandler().Handle(new CreateProjectCommand("P", null, null), CancellationToken.None);

        result.Error.ShouldBe(ProjectErrors.InvalidOwner);
        _projects.DidNotReceive().Add(Arg.Any<Project>());
    }

    [Fact]
    public async Task Create_AsAdminWithTeamMemberOwner_ReturnsInvalidOwner()
    {
        SetCaller(Guid.NewGuid(), Role.Admin);
        var member = NewUser(Role.TeamMember);

        var result = await CreateHandler().Handle(new CreateProjectCommand("P", null, member.Id), CancellationToken.None);

        result.Error.ShouldBe(ProjectErrors.InvalidOwner);
        _projects.DidNotReceive().Add(Arg.Any<Project>());
    }

    [Fact]
    public async Task Create_AsAdminWithUnknownOwner_ReturnsInvalidOwner()
    {
        SetCaller(Guid.NewGuid(), Role.Admin);

        var result = await CreateHandler().Handle(
            new CreateProjectCommand("P", null, Guid.NewGuid()), CancellationToken.None);

        result.Error.ShouldBe(ProjectErrors.InvalidOwner);
    }

    [Fact]
    public async Task Create_AsAdminWithProjectManagerOwner_AddsProjectAndSaves()
    {
        SetCaller(Guid.NewGuid(), Role.Admin);
        var pm = NewUser(Role.ProjectManager);
        Project? added = null;
        _projects.When(r => r.Add(Arg.Any<Project>())).Do(c => added = c.Arg<Project>());

        var result = await CreateHandler().Handle(new CreateProjectCommand("P", "d", pm.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        added.ShouldNotBeNull();
        added.OwnerId.ShouldBe(pm.Id);
        result.Value.ShouldBe(added.Id);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_AsProjectManagerWithoutOwner_OwnerIsCaller()
    {
        var caller = Guid.NewGuid();
        SetCaller(caller, Role.ProjectManager);

        var result = await CreateHandler().Handle(new CreateProjectCommand("P", null, null), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _projects.Received(1).Add(Arg.Is<Project>(p => p.OwnerId == caller));
    }

    [Fact]
    public async Task Create_AsProjectManagerWithOwnId_OwnerIsCaller()
    {
        var caller = Guid.NewGuid();
        SetCaller(caller, Role.ProjectManager);

        var result = await CreateHandler().Handle(new CreateProjectCommand("P", null, caller), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _projects.Received(1).Add(Arg.Is<Project>(p => p.OwnerId == caller));
    }

    [Fact]
    public async Task Create_AsProjectManagerForAnotherUser_ReturnsOwnerNotAllowedAndAddsNothing()
    {
        SetCaller(Guid.NewGuid(), Role.ProjectManager);

        var result = await CreateHandler().Handle(
            new CreateProjectCommand("P", null, Guid.NewGuid()), CancellationToken.None);

        result.Error.ShouldBe(ProjectErrors.OwnerNotAllowed);
        _projects.DidNotReceive().Add(Arg.Any<Project>());
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Create_NoUserIdInSecurityContext_ReturnsTokenRevoked()
    {
        _security.UserId.Returns((Guid?)null);
        _security.Role.Returns(Role.Admin);

        var result = await CreateHandler().Handle(new CreateProjectCommand("P", null, null), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.TokenRevoked);
    }

    [Fact]
    public async Task Update_AsProjectManagerOfAnotherPmsProject_ReturnsNotFoundAndSavesNothing()
    {
        SetCaller(Guid.NewGuid(), Role.ProjectManager);
        var project = NewProject(Guid.NewGuid());

        var result = await new UpdateProjectCommandHandler(_security, _projects, _unitOfWork)
            .Handle(new UpdateProjectCommand(project.Id, "New", null), CancellationToken.None);

        result.Error.ShouldBe(ProjectErrors.NotFound);
        project.Name.ShouldBe("P");
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Update_AsAdmin_UpdatesAnyProject()
    {
        SetCaller(Guid.NewGuid(), Role.Admin);
        var project = NewProject(Guid.NewGuid());

        var result = await new UpdateProjectCommandHandler(_security, _projects, _unitOfWork)
            .Handle(new UpdateProjectCommand(project.Id, "New", "desc"), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        project.Name.ShouldBe("New");
        project.Description.ShouldBe("desc");
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_AsProjectManagerOfAnotherPmsProject_ReturnsNotFound()
    {
        SetCaller(Guid.NewGuid(), Role.ProjectManager);
        var project = NewProject(Guid.NewGuid());

        var result = await DeleteHandler()
            .Handle(new DeleteProjectCommand(project.Id), CancellationToken.None);

        result.Error.ShouldBe(ProjectErrors.NotFound);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Delete_AsOwner_SoftDeletesWithTimeProviderTime()
    {
        var owner = Guid.NewGuid();
        SetCaller(owner, Role.ProjectManager);
        var project = NewProject(owner);

        var result = await DeleteHandler()
            .Handle(new DeleteProjectCommand(project.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        project.IsDeleted.ShouldBeTrue();
        project.DeletedAt.ShouldBe(_time.GetUtcNow());
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeOwner_ToNonProjectManager_ReturnsInvalidOwner()
    {
        SetCaller(Guid.NewGuid(), Role.Admin);
        var project = NewProject(Guid.NewGuid());
        var member = NewUser(Role.TeamMember);

        var result = await OwnerHandler().Handle(
            new ChangeProjectOwnerCommand(project.Id, member.Id), CancellationToken.None);

        result.Error.ShouldBe(ProjectErrors.InvalidOwner);
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeOwner_ToCurrentOwner_SucceedsWithoutSave()
    {
        SetCaller(Guid.NewGuid(), Role.Admin);
        var pm = NewUser(Role.ProjectManager);
        var project = NewProject(pm.Id);

        var result = await OwnerHandler().Handle(
            new ChangeProjectOwnerCommand(project.Id, pm.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        await _unitOfWork.DidNotReceive().SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifier.DidNotReceiveWithAnyArgs().ProjectOwnerChangedAsync(default!, default, default, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeOwner_ToOtherProjectManager_ChangesOwnerAndSaves()
    {
        var caller = Guid.NewGuid();
        SetCaller(caller, Role.Admin);
        var pm = NewUser(Role.ProjectManager);
        var previousOwner = Guid.NewGuid();
        var project = NewProject(previousOwner);

        var result = await OwnerHandler().Handle(
            new ChangeProjectOwnerCommand(project.Id, pm.Id), CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        project.OwnerId.ShouldBe(pm.Id);
        await _unitOfWork.Received(1).SaveChangesAsync(Arg.Any<CancellationToken>());
        await _notifier.Received(1).ProjectOwnerChangedAsync(project, previousOwner, caller, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task ChangeOwner_UnknownProject_ReturnsNotFound()
    {
        SetCaller(Guid.NewGuid(), Role.Admin);

        var result = await OwnerHandler().Handle(
            new ChangeProjectOwnerCommand(Guid.NewGuid(), Guid.NewGuid()), CancellationToken.None);

        result.Error.ShouldBe(ProjectErrors.NotFound);
    }
}
