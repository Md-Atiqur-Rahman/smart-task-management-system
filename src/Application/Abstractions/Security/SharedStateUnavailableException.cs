using System.Diagnostics.CodeAnalysis;

using Datavanced.SmartTaskManagement.Domain.Exceptions;

namespace Datavanced.SmartTaskManagement.Application.Abstractions.Security;

/// <summary>
/// The shared security state (Redis) is unreachable or timed out. Security checks that
/// need it fail closed; the Api maps this to 503 (ADR 0013).
/// </summary>
[SuppressMessage(
    "Design",
    "CA1032:Implement standard exception constructors",
    Justification = "Like the Domain exceptions: the standard constructors would create an exception without ErrorCode and Title, which the error model forbids.")]
public sealed class SharedStateUnavailableException()
    : AppException(
        "shared_state.unavailable",
        "Shared state unavailable",
        "The shared security state store is unreachable or timed out.");
