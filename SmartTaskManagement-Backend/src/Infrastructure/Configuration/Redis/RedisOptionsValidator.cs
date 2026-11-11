using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

using StackExchange.Redis;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Redis;

/// <summary>Rejects an unencrypted Redis connection (no ssl=true) outside Development and Testing.</summary>
public sealed class RedisOptionsValidator(IHostEnvironment environment)
    : IValidateOptions<RedisOptions>
{
    public const string TestingEnvironment = "Testing";

    public ValidateOptionsResult Validate(string? name, RedisOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ConnectionString))
        {
            // [Required] reports this one.
            return ValidateOptionsResult.Success;
        }

        ConfigurationOptions parsed;
        try
        {
            parsed = ConfigurationOptions.Parse(options.ConnectionString);
        }
        catch (ArgumentException)
        {
            return ValidateOptionsResult.Fail("Redis:ConnectionString is not a valid StackExchange.Redis connection string.");
        }

        var unencryptedAllowed = environment.IsDevelopment()
            || environment.IsEnvironment(TestingEnvironment);

        return parsed.Ssl || unencryptedAllowed
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(
                $"Redis without ssl=true is allowed only in Development and {TestingEnvironment} "
                + $"(current environment: {environment.EnvironmentName}).");
    }
}
