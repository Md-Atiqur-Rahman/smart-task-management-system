// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Domain.Results;

namespace Contoso.TemplateApp.Domain.Samples;

/// <summary>Expected failures of the Sample aggregate. Codes are part of the API contract.</summary>
public static class SampleErrors
{
    public static readonly Error NotFound =
        Error.NotFound("sample.not_found", "The sample was not found.");

    public static readonly Error AlreadyCompleted =
        Error.Conflict("sample.already_completed", "The sample is already completed.");
}
