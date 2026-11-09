using System.Security.Claims;

using Datavanced.SmartTaskManagement.Application.Abstractions.Security;

namespace Datavanced.SmartTaskManagement.UnitTests.Application.Security;

public sealed class SecurityContextTests
{
    private const string AuthenticationType = "Test";

    [Fact]
    public void ValidPrincipal_SetsUserIdAndRoles()
    {
        var userId = Guid.NewGuid();
        var context = new SecurityContext();

        context.SetPrincipal(Principal(
            new Claim(SecurityClaimTypes.UserId, userId.ToString()),
            new Claim(SecurityClaimTypes.Role, "Admin")));

        context.IsAuthenticated.ShouldBeTrue();
        context.UserId.ShouldBe(userId);
        context.Roles.ShouldBe(["Admin"]);
    }

    [Fact]
    public void SingleKnownRoleClaim_SetsRole()
    {
        var context = new SecurityContext();

        context.SetPrincipal(Principal(
            new Claim(SecurityClaimTypes.UserId, Guid.NewGuid().ToString()),
            new Claim(SecurityClaimTypes.Role, "ProjectManager")));

        context.Role.ShouldBe(Datavanced.SmartTaskManagement.Domain.Users.Role.ProjectManager);
    }

    [Theory]
    [InlineData("Nobody")]
    [InlineData("7")]
    [InlineData("1")]
    [InlineData("Admin,TeamMember")]
    public void UnknownRoleClaim_LeavesRoleNull(string value)
    {
        var context = new SecurityContext();

        context.SetPrincipal(Principal(
            new Claim(SecurityClaimTypes.UserId, Guid.NewGuid().ToString()),
            new Claim(SecurityClaimTypes.Role, value)));

        context.Role.ShouldBeNull();
    }

    [Fact]
    public void MissingUserIdClaim_IsUnauthenticated()
    {
        var context = new SecurityContext();

        context.SetPrincipal(Principal(new Claim(SecurityClaimTypes.Role, "Admin")));

        context.IsAuthenticated.ShouldBeFalse();
        context.UserId.ShouldBeNull();
        context.Roles.ShouldBeEmpty();
    }

    [Fact]
    public void MalformedUserIdClaim_IsUnauthenticated()
    {
        var context = new SecurityContext();

        context.SetPrincipal(Principal(new Claim(SecurityClaimTypes.UserId, "not-a-guid")));

        context.IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public void AnonymousPrincipal_IsUnauthenticated()
    {
        var context = new SecurityContext();

        context.SetPrincipal(new ClaimsPrincipal(new ClaimsIdentity()));

        context.IsAuthenticated.ShouldBeFalse();
    }

    [Fact]
    public void SetPrincipal_CalledTwice_Throws()
    {
        var context = new SecurityContext();
        context.SetPrincipal(new ClaimsPrincipal(new ClaimsIdentity()));

        Should.Throw<InvalidOperationException>(
            () => context.SetPrincipal(new ClaimsPrincipal(new ClaimsIdentity())));
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) =>
        new(new ClaimsIdentity(claims, AuthenticationType));
}
