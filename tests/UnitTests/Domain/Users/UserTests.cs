using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.UnitTests.Domain.Users;

public sealed class UserTests
{
    [Fact]
    public void Register_ValidInput_IsTeamMemberWithVersion1()
    {
        var user = User.Register(Email.Create("a@x.com"), "Rahim", "hash");

        user.Role.ShouldBe(Role.TeamMember);
        user.TokenVersion.ShouldBe(1);
        user.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public void CreateSeedAdmin_ValidInput_IsAdminWithVersion1()
    {
        var user = User.CreateSeedAdmin(Email.Create("a@x.com"), "Admin", "hash");

        user.Role.ShouldBe(Role.Admin);
        user.TokenVersion.ShouldBe(1);
    }

    [Fact]
    public void Register_FullNameTooLong_Throws()
    {
        Should.Throw<ArgumentException>(
            () => User.Register(Email.Create("a@x.com"), new string('n', User.FullNameMaxLength + 1), "hash"));
    }

    [Fact]
    public void ChangeRole_DifferentRole_ChangesRoleAndIncrementsVersion()
    {
        var user = User.Register(Email.Create("a@x.com"), "Rahim", "hash");

        user.ChangeRole(Role.ProjectManager).ShouldBeTrue();

        user.Role.ShouldBe(Role.ProjectManager);
        user.TokenVersion.ShouldBe(2);
    }

    [Fact]
    public void ChangeRole_SameRole_ReturnsFalseAndKeepsVersion()
    {
        var user = User.Register(Email.Create("a@x.com"), "Rahim", "hash");

        user.ChangeRole(Role.TeamMember).ShouldBeFalse();

        user.TokenVersion.ShouldBe(1);
    }
}
