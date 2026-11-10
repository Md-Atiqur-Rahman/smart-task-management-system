namespace Datavanced.SmartTaskManagement.Application.Abstractions.Security;

/// <summary>
/// Shared (cross-instance) copy of each user's current token version; SQL Server stays the
/// source of truth (ADR 0013). Entries only move forward and expire a fixed time after the
/// last write; a read never extends the expiry. Every method throws
/// <see cref="SharedStateUnavailableException"/> when the store is unreachable, times out or returns an error.
/// </summary>
public interface ITokenVersionStore
{
    /// <summary>The stored version, or null when the user has no entry.</summary>
    Task<int?> GetAsync(Guid userId, CancellationToken ct);

    /// <summary>Stores the version only when the entry is missing or lower, and then (re)sets the fixed expiry.</summary>
    Task SetIfHigherAsync(Guid userId, int version, CancellationToken ct);
}
