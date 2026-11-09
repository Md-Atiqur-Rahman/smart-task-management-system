using System.Security.Claims;

using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Abstractions.Security;

/// <summary>
/// Scoped per request. Registered once and exposed as both
/// <see cref="ISecurityContext"/> (reader) and <see cref="ISecurityContextSetter"/>.
/// </summary>
internal sealed class SecurityContext : ISecurityContext, ISecurityContextSetter
{
    private bool _isSet;

    public bool IsAuthenticated => UserId is not null;

    public Guid? UserId { get; private set; }

    public Role? Role { get; private set; }

    public IReadOnlyCollection<string> Roles { get; private set; } = [];

    public void SetPrincipal(ClaimsPrincipal principal)
    {
        if (_isSet)
        {
            throw new InvalidOperationException("The security context can be set only once per request.");
        }

        _isSet = true;

        if (principal.Identity?.IsAuthenticated != true
            || !Guid.TryParse(principal.FindFirst(SecurityClaimTypes.UserId)?.Value, out var userId))
        {
            return;
        }

        UserId = userId;
        Roles = principal.FindAll(SecurityClaimTypes.Role).Select(c => c.Value).ToArray();
        Role = Roles.Count == 1
            && Enum.GetNames<Role>().Contains(Roles.First(), StringComparer.Ordinal)
            && Enum.TryParse<Role>(Roles.First(), ignoreCase: false, out var role)
            ? role
            : null;
    }
}
