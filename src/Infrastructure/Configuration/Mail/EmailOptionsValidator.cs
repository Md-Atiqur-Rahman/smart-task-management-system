using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Mail;

/// <summary>
/// Rejects SMTP without TLS outside Development and Testing (same rule as ADR 0011), link templates
/// without {id}, a relative AppBaseUrl and a user name without a password.
/// </summary>
public sealed class EmailOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<EmailOptions>
{
    public const string TestingEnvironment = "Testing";

    public ValidateOptionsResult Validate(string? name, EmailOptions options)
    {
        var failures = new List<string>();

        var unencryptedAllowed = environment.IsDevelopment()
            || environment.IsEnvironment(TestingEnvironment);
        if (!options.UseTls && !unencryptedAllowed)
        {
            failures.Add(
                $"Email:UseTls=false is allowed only in Development and {TestingEnvironment} "
                + $"(current environment: {environment.EnvironmentName}).");
        }

        CheckTemplate(nameof(EmailOptions.TaskPathTemplate), options.TaskPathTemplate, failures);
        CheckTemplate(nameof(EmailOptions.ProjectPathTemplate), options.ProjectPathTemplate, failures);

        // A missing value is reported by [Required].
        if (options.AppBaseUrl is { } baseUrl
            && (!baseUrl.IsAbsoluteUri
                || (baseUrl.Scheme != Uri.UriSchemeHttp && baseUrl.Scheme != Uri.UriSchemeHttps)))
        {
            failures.Add("Email:AppBaseUrl must be an absolute http or https URL.");
        }

        if (!string.IsNullOrWhiteSpace(options.UserName) && string.IsNullOrEmpty(options.Password))
        {
            failures.Add("Email:Password is required when Email:UserName is set (user-secrets or environment only).");
        }

        return failures.Count == 0 ? ValidateOptionsResult.Success : ValidateOptionsResult.Fail(failures);
    }

    private static void CheckTemplate(string key, string template, List<string> failures)
    {
        if (!string.IsNullOrWhiteSpace(template)
            && !template.Contains(EmailOptions.IdPlaceholder, StringComparison.Ordinal))
        {
            failures.Add($"Email:{key} must contain {EmailOptions.IdPlaceholder}.");
        }
    }
}
