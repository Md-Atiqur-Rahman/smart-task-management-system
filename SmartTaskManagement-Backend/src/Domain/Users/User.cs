using Datavanced.SmartTaskManagement.Domain.Primitives;

namespace Datavanced.SmartTaskManagement.Domain.Users;

/// <summary>Account aggregate: identity, credentials hash, role and token version.</summary>
public sealed class User : AuditableAggregateRoot<Guid>
{
    public const int FullNameMaxLength = 100;

    // Used by the factories and by EF Core materialization (constructor binding by name).
    private User(Guid id, Email email, string fullName, string passwordHash, Role role, int tokenVersion)
        : base(id)
    {
        Email = email;
        FullName = fullName;
        PasswordHash = passwordHash;
        Role = role;
        TokenVersion = tokenVersion;
    }

    public Email Email { get; private set; }

    public string FullName { get; private set; }

    public string PasswordHash { get; private set; }

    public Role Role { get; private set; }

    public int TokenVersion { get; private set; }

    public static User Register(Email email, string fullName, string passwordHash) =>
        Build(email, fullName, passwordHash, Role.TeamMember);

    public static User CreateSeedAdmin(Email email, string fullName, string passwordHash) =>
        Build(email, fullName, passwordHash, Role.Admin);

    /// <summary>Returns true when the role changed; the token version then increases so old tokens stop working.</summary>
    public bool ChangeRole(Role newRole)
    {
        if (Role == newRole)
        {
            return false;
        }

        Role = newRole;
        TokenVersion++;
        return true;
    }

    private static User Build(Email email, string fullName, string passwordHash, Role role)
    {
        Guard.NotNullOrWhiteSpace(fullName);
        Guard.NotNullOrWhiteSpace(passwordHash);

        var trimmedName = fullName.Trim();
        Guard.MaxLength(trimmedName, FullNameMaxLength, nameof(fullName));

        return new User(Guid.CreateVersion7(), email, trimmedName, passwordHash, role, tokenVersion: 1);
    }
}
