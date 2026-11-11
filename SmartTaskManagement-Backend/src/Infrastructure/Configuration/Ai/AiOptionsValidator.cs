using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Ai;

/// <summary>Checks the nested Gemini settings that data annotations do not reach: a model id and an https base URL.</summary>
public sealed class AiOptionsValidator : IValidateOptions<AiOptions>
{
    public ValidateOptionsResult Validate(string? name, AiOptions options)
    {
        var failures = new List<string>();

        if (string.IsNullOrWhiteSpace(options.Gemini.Model))
        {
            failures.Add("Ai:Gemini:Model is required.");
        }

        if (!options.Gemini.BaseUrl.IsAbsoluteUri || options.Gemini.BaseUrl.Scheme != Uri.UriSchemeHttps)
        {
            failures.Add("Ai:Gemini:BaseUrl must be an absolute https URL.");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }
}
