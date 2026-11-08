// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Domain.Samples;

/// <summary>Expected failures of the Sample aggregate. Codes are part of the API contract.</summary>
public static class SampleErrors
{
    public static readonly Error NotFound =
        Error.NotFound("sample.not_found", "The sample was not found.");

    public static readonly Error AlreadyCompleted =
        Error.Conflict("sample.already_completed", "The sample is already completed.");
}
