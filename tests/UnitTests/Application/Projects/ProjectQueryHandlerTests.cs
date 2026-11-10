using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Application.Features.Projects;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Queries.GetProjectById;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Queries.ListProjects;
using Datavanced.SmartTaskManagement.Domain.Auth;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Users;

using Microsoft.Extensions.Options;

using NSubstitute;

using SortDirection = Datavanced.SmartTaskManagement.Application.Abstractions.Paging.SortDirection;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Projects;

public sealed class ProjectQueryHandlerTests
{
    private readonly IProjectReadRepository _readRepository = Substitute.For<IProjectReadRepository>();
    private readonly ISecurityContext _security = Substitute.For<ISecurityContext>();

    private static ListProjectsQuery NewListQuery(string? sortBy = null) =>
        new(null, null, sortBy, SortDirection.Ascending, 1, 20);

    private static ListProjectsQueryValidator NewValidator() =>
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
        SetCaller(callerId, Role.ProjectManager);
        var handler = new GetProjectByIdQueryHandler(_security, _readRepository);

        var result = await handler.Handle(new GetProjectByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Error.ShouldBe(ProjectErrors.NotFound);
    }

    [Fact]
    public async Task GetById_Visible_ReturnsDtoForCaller()
    {
        var callerId = Guid.NewGuid();
        SetCaller(callerId, Role.Admin);
        var dto = new ProjectDetailsDto(
            Guid.NewGuid(), "P", null, new ProjectOwnerDto(Guid.NewGuid(), "Owner", "o@x.com"),
            DateTimeOffset.UnixEpoch, null);
        _readRepository.GetByIdAsync(dto.Id, callerId, Role.Admin, Arg.Any<CancellationToken>()).Returns(dto);
        var handler = new GetProjectByIdQueryHandler(_security, _readRepository);

        var result = await handler.Handle(new GetProjectByIdQuery(dto.Id), CancellationToken.None);

        result.Value.ShouldBe(dto);
    }

    [Fact]
    public async Task GetById_NoUserInSecurityContext_ReturnsTokenRevoked()
    {
        SetCaller(null, Role.Admin);
        var handler = new GetProjectByIdQueryHandler(_security, _readRepository);

        var result = await handler.Handle(new GetProjectByIdQuery(Guid.NewGuid()), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.TokenRevoked);
        await _readRepository.DidNotReceive().GetByIdAsync(
            Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<Role>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_PassesCallerIdAndRoleToRepository()
    {
        var callerId = Guid.NewGuid();
        SetCaller(callerId, Role.ProjectManager);
        var query = NewListQuery();
        var page = new PagedResult<ProjectListItemDto>([], 1, 20, 0);
        _readRepository.ListAsync(query, callerId, Role.ProjectManager, Arg.Any<CancellationToken>()).Returns(page);
        var handler = new ListProjectsQueryHandler(_security, _readRepository);

        var result = await handler.Handle(query, CancellationToken.None);

        result.Value.ShouldBe(page);
        await _readRepository.Received(1).ListAsync(query, callerId, Role.ProjectManager, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task List_NoRoleInSecurityContext_ReturnsTokenRevoked()
    {
        SetCaller(Guid.NewGuid(), null);
        var handler = new ListProjectsQueryHandler(_security, _readRepository);

        var result = await handler.Handle(NewListQuery(), CancellationToken.None);

        result.Error.ShouldBe(AuthErrors.TokenRevoked);
        await _readRepository.DidNotReceive().ListAsync(
            Arg.Any<ListProjectsQuery>(), Arg.Any<Guid>(), Arg.Any<Role>(), Arg.Any<CancellationToken>());
    }

    [Theory]
    [InlineData("name")]
    [InlineData("createdAt")]
    [InlineData("updatedAt")]
    public void ListValidator_WhitelistedSortBy_Passes(string sortBy)
    {
        NewValidator().Validate(NewListQuery(sortBy)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void ListValidator_UnknownSortBy_Fails()
    {
        var result = NewValidator().Validate(NewListQuery("password"));

        result.Errors.ShouldContain(e => e.PropertyName == nameof(ListQuery.SortBy));
    }
}
