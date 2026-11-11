using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Abstractions.Security;

/// <summary>
/// Read-only identity of the current request. Comes only from the validated
/// token; a missing claim means unauthenticated, never a default value.
/// </summary>
public interface ISecurityContext
{
    bool IsAuthenticated { get; }

    Guid? UserId { get; }

    /// <summary>The user's single role (PRD 2), or null when unauthenticated or the claim is missing or unknown.</summary>
    Role? Role { get; }

    IReadOnlyCollection<string> Roles { get; }
}
