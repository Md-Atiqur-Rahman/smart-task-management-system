namespace Datavanced.SmartTaskManagement.Application.Features.Users;

/// <summary>Read side of users: returns plain values and DTOs only, never aggregates.</summary>
public interface IUserReadRepository
{
    /// <summary>The user's current token version, or null when the user does not exist.</summary>
    Task<int?> GetTokenVersionAsync(Guid userId, CancellationToken ct);
}
