using System.Diagnostics.CodeAnalysis;

using Datavanced.SmartTaskManagement.Domain.Primitives;
using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Domain.Auth;

/// <summary>Server-side record of an issued refresh token. Only the hash of the token is stored.</summary>
public sealed class RefreshToken : AggregateRoot<Guid>
{
    /// <summary>Length of the hex-encoded SHA-256 token hash.</summary>
    public const int TokenHashLength = 64;

    // Used by Issue and by EF Core materialization (constructor binding by name).
    private RefreshToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTimeOffset createdAt,
        DateTimeOffset expiresAt,
        DateTimeOffset? revokedAt,
        Guid? replacedById)
        : base(id)
    {
        UserId = userId;
        TokenHash = tokenHash;
        CreatedAt = createdAt;
        ExpiresAt = expiresAt;
        RevokedAt = revokedAt;
        ReplacedById = replacedById;
    }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedById { get; private set; }

    [SuppressMessage(
        "Performance",
        "CA1819:Properties should not return arrays",
        Justification = "EF Core maps the SQL Server rowversion concurrency token to byte[].")]
    public byte[] RowVersion { get; private set; } = [];

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime)
    {
        Guard.NotNullOrWhiteSpace(tokenHash);

        if (tokenHash.Length != TokenHashLength)
        {
            throw new ArgumentException($"Must be exactly {TokenHashLength} characters.", nameof(tokenHash));
        }

        if (lifetime <= TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(lifetime), lifetime, "Must be positive.");
        }

        return new RefreshToken(Guid.CreateVersion7(), userId, tokenHash, now, now.Add(lifetime), revokedAt: null, replacedById: null);
    }

    /// <summary>Revokes the token; <paramref name="replacedById"/> is set when a rotation replaces it.</summary>
    public Result Revoke(DateTimeOffset now, Guid? replacedById)
    {
        if (RevokedAt is not null)
        {
            return Result.Failure(AuthErrors.InvalidRefreshToken);
        }

        RevokedAt = now;
        ReplacedById = replacedById;
        return Result.Success();
    }

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && now < ExpiresAt;

    /// <summary>True when the token was revoked by rotation less than <paramref name="grace"/> ago.</summary>
    public bool IsWithinRotationGrace(DateTimeOffset now, TimeSpan grace) =>
        ReplacedById is not null && RevokedAt is { } revokedAt && now - revokedAt < grace;
}
