using System.Diagnostics.CodeAnalysis;

namespace Datavanced.SmartTaskManagement.Domain.Results;

/// <summary>An expected failure: machine-readable code, human description, type.</summary>
[SuppressMessage(
    "Naming",
    "CA1716:Identifiers should not match keywords",
    Justification = "'Error' is a keyword only in Visual Basic; this C#-only code base has no VB consumers, and Error is the conventional name in the Result pattern.")]
public record Error(string Code, string Description, ErrorType Type)
{
    public static Error Validation(string code, string description) =>
        new(code, description, ErrorType.Validation);

    public static Error NotFound(string code, string description) =>
        new(code, description, ErrorType.NotFound);

    public static Error Conflict(string code, string description) =>
        new(code, description, ErrorType.Conflict);

    public static Error Forbidden(string code, string description) =>
        new(code, description, ErrorType.Forbidden);

    public static Error Unauthorized(string code, string description) =>
        new(code, description, ErrorType.Unauthorized);

    public static Error Unavailable(string code, string description) =>
        new(code, description, ErrorType.Unavailable);
}
