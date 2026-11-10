using Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.ChangeProjectOwner;
using Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.CreateProject;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Projects;

public sealed class ProjectCommandValidatorTests
{
    private readonly CreateProjectCommandValidator _create = new();

    [Fact]
    public void CreateValidator_WhitespaceName_Fails()
    {
        _create.Validate(new CreateProjectCommand("   ", null, null)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void CreateValidator_TwoHundredCharNameWithSpaces_Passes()
    {
        var name = "  " + new string('a', 200) + "  ";

        _create.Validate(new CreateProjectCommand(name, null, null)).IsValid.ShouldBeTrue();
    }

    [Fact]
    public void CreateValidator_EmptyGuidOwner_Fails()
    {
        _create.Validate(new CreateProjectCommand("P", null, Guid.Empty)).IsValid.ShouldBeFalse();
    }

    [Fact]
    public void ChangeOwnerValidator_EmptyGuid_Fails()
    {
        new ChangeProjectOwnerCommandValidator()
            .Validate(new ChangeProjectOwnerCommand(Guid.NewGuid(), Guid.Empty)).IsValid.ShouldBeFalse();
    }
}
