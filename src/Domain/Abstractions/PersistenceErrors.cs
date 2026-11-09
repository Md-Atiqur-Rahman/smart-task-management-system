using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Domain.Abstractions;

/// <summary>Expected persistence failures returned by <see cref="IUnitOfWork"/>. Codes are part of the API contract.</summary>
public static class PersistenceErrors
{
    public const string DuplicateKeyCode = "persistence.duplicate_key";

    public static readonly Error DuplicateKey = Error.Conflict(
        DuplicateKeyCode,
        "A record with the same unique value already exists.");
}
