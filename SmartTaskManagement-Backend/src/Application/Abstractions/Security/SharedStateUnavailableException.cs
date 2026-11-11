using System.Diagnostics.CodeAnalysis;

using Datavanced.SmartTaskManagement.Domain.Exceptions;

namespace Datavanced.SmartTaskManagement.Application.Abstractions.Security;

/// <summary>
/// The shared security state (Redis) is unreachable, timed out or returned an error. The global
/// exception handler does not map it: each caller catches it and decides. The version check and
/// a role change fail closed (503 auth.unavailable), publishing a version after login only logs,
/// and the rate limiter fails open (ADR 0013).
/// </summary>
[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Like the Domain exceptions: the standard constructors would create an exception without ErrorCode and Title, which the error model forbids.")]
public sealed class SharedStateUnavailableException(Exception? innerException = null)
    : AppException(
        "shared_state.unavailable",
        "Shared state unavailable",
        "The shared security state store is unreachable or timed out.",
        innerException);
