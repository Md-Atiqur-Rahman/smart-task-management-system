using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Contoso.TemplateApp.Infrastructure.Configuration.Database;

/// <summary>Rejects an unencrypted database connection outside Development and Testing.</summary>
public sealed class DatabaseOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<DatabaseOptions>
{
    public const string TestingEnvironment = "Testing";

    public ValidateOptionsResult Validate(string? name, DatabaseOptions options)
    {
        var unencryptedAllowed = environment.IsDevelopment()
            || environment.IsEnvironment(TestingEnvironment);

        return options.Encrypt || unencryptedAllowed
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Database:Encrypt=false is allowed only in Development and {TestingEnvironment} "
                + $"(current environment: {environment.EnvironmentName}).");
    }
}
