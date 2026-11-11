using System.Diagnostics.CodeAnalysis;

using Datavanced.SmartTaskManagement.Domain.Exceptions;

namespace Datavanced.SmartTaskManagement.Infrastructure.Seeding;

/// <summary>The first Admin could not be seeded; startup fails. Never carries the password.</summary>
[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Like the Domain exceptions: the standard constructors would create an exception without ErrorCode and Title, which the error model forbids.")]
public sealed class AdminSeedException(string errorCode, string message)
    : AppException(errorCode, "Admin seeding failed", message);
