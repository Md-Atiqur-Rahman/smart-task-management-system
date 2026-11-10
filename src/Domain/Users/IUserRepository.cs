namespace Datavanced.SmartTaskManagement.Domain.Users;

/// <summary>Write repository of the User aggregate. Changes are saved by IUnitOfWork.</summary>
public interface IUserRepository
{
    /// <summary>Tracked aggregate, or null when it does not exist or is soft-deleted.</summary>
    Task<User?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<User?> GetByEmailAsync(Email email, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(Email email, CancellationToken cancellationToken);

    Task<int> CountByRoleAsync(Role role, CancellationToken cancellationToken);

    void Add(User user);
}
