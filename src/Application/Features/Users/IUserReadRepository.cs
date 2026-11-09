using Datavanced.SmartTaskManagement.Application.Features.Auth;

namespace Datavanced.SmartTaskManagement.Application.Features.Users;

/// <summary>Read side of users: returns plain values and DTOs only, never aggregates.</summary>
public interface IUserReadRepository
{
    /// <summary>The user's current token version, or null when the user does not exist.</summary>
    Task<int?> GetTokenVersionAsync(Guid userId, CancellationToken ct);

    /// <summary>The user as shown to the signed-in client, or null when the user does not exist.</summary>
    Task<CurrentUserDto?> GetCurrentUserAsync(Guid id, CancellationToken ct);
}
