using Datavanced.SmartTaskManagement.Domain.Projects;

namespace Datavanced.SmartTaskManagement.UnitTests.Domain.Projects;

public sealed class ProjectTests
{
    private static readonly Guid Owner = Guid.NewGuid();

    [Fact]
    public void Create_ValidInput_TrimsAndKeepsOwner()
    {
        var project = Project.Create("  Alpha  ", "  first  ", Owner);

        project.Name.ShouldBe("Alpha");
        project.Description.ShouldBe("first");
        project.OwnerId.ShouldBe(Owner);
        project.Id.ShouldNotBe(Guid.Empty);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_DescriptionEmptyAfterTrim_IsNull(string? description) =>
        Project.Create("Alpha", description, Owner).Description.ShouldBeNull();

    [Fact]
    public void Create_NameWithSpacesAroundMaxLength_IsAccepted() =>
        Project.Create($"  {new string('n', 200)}  ", null, Owner).Name.Length.ShouldBe(200);

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_BlankName_Throws(string name) =>
        Should.Throw<ArgumentException>(() => Project.Create(name, null, Owner));

    [Fact]
    public void Create_NameTooLong_Throws() =>
        Should.Throw<ArgumentException>(() => Project.Create(new string('n', 201), null, Owner));

    [Fact]
    public void Create_DescriptionTooLong_Throws() =>
        Should.Throw<ArgumentException>(() => Project.Create("A", new string('d', 2001), Owner));

    [Fact]
    public void Create_EmptyOwner_Throws() =>
        Should.Throw<ArgumentException>(() => Project.Create("A", null, Guid.Empty));

    [Fact]
    public void UpdateDetails_NewValues_AreTrimmedAndStored()
    {
        var project = Project.Create("Alpha", "first", Owner);

        project.UpdateDetails(" Beta ", "   ");

        project.Name.ShouldBe("Beta");
        project.Description.ShouldBeNull();
    }

    [Fact]
    public void ChangeOwner_DifferentOwner_ReturnsTrueAndChanges()
    {
        var project = Project.Create("Alpha", null, Owner);
        var other = Guid.NewGuid();

        project.ChangeOwner(other).ShouldBeTrue();

        project.OwnerId.ShouldBe(other);
    }

    [Fact]
    public void ChangeOwner_SameOwner_ReturnsFalse()
    {
        var project = Project.Create("Alpha", null, Owner);

        project.ChangeOwner(Owner).ShouldBeFalse();

        project.OwnerId.ShouldBe(Owner);
    }

    [Fact]
    public void ChangeOwner_EmptyGuid_Throws()
    {
        var project = Project.Create("Alpha", null, Owner);

        Should.Throw<ArgumentException>(() => project.ChangeOwner(Guid.Empty));
    }

    [Fact]
    public void Delete_SetsDeletedAt()
    {
        var project = Project.Create("Alpha", null, Owner);
        var at = new DateTimeOffset(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

        project.Delete(at);

        project.IsDeleted.ShouldBeTrue();
        project.DeletedAt.ShouldBe(at);
    }
}
