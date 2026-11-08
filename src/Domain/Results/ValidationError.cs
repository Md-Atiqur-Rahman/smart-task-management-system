namespace Contoso.TemplateApp.Domain.Results;

/// <summary>Validation failure with messages per field.</summary>
public sealed record ValidationError(IReadOnlyDictionary<string, string[]> Errors)
    : Error(ValidationError.ErrorCode, ValidationError.ErrorDescription, ErrorType.Validation)
{
    public const string ErrorCode = "validation.failed";
    public const string ErrorDescription = "One or more validation errors occurred.";
}
