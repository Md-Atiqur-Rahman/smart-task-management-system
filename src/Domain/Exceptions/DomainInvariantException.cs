namespace Datavanced.SmartTaskManagement.Domain.Exceptions;

/// <summary>A domain invariant was violated by code, not by user input.</summary>
public sealed class DomainInvariantException(string errorCode, string message)
    : AppException(errorCode, "Domain invariant violated", message);
