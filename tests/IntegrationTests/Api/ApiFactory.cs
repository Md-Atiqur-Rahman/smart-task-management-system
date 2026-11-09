using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Database;
using Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using Serilog.Core;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api;

/// <summary>
/// The real Program in the "Testing" environment with test-only configuration.
/// Settings can be overridden per test (for example a real database from
/// SqlServerFixture, or a missing secret). Adds the test-only controllers and a
/// capturing Serilog sink.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>
{
    private readonly Dictionary<string, string?> _settings = new(StringComparer.Ordinal)
    {
        ["Database:Host"] = "localhost",
        ["Database:Port"] = "1433",
        ["Database:Name"] = "not-used-without-database",
        ["Database:User"] = "sa",
        ["Database:Password"] = "not-used-without-database",
        ["Database:Encrypt"] = "false",
        ["Redis:ConnectionString"] = "127.0.0.1:1,password=not-used-without-redis",
        ["Jwt:Issuer"] = TestTokens.Issuer,
        ["Jwt:Audience"] = TestTokens.Audience,
        ["Jwt:SigningKey"] = TestTokens.SigningKey,
    };

    private string _environment = DatabaseOptionsValidator.TestingEnvironment;

    private bool _useRealRedis;

    public CapturingSink LogSink { get; } = new();

    /// <summary>The in-memory token version store used unless <see cref="WithRedis"/> is called.</summary>
    public InMemoryTokenVersionStore TokenVersions { get; } = new();

    public ApiFactory WithEnvironment(string environment)
    {
        _environment = environment;
        return this;
    }

    public ApiFactory WithSetting(string key, string? value)
    {
        _settings[key] = value;
        return this;
    }

    public ApiFactory WithDatabase(DatabaseOptions database)
    {
        _settings["Database:Host"] = database.Host;
        _settings["Database:Port"] = database.Port.ToString(System.Globalization.CultureInfo.InvariantCulture);
        _settings["Database:Name"] = database.Name;
        _settings["Database:User"] = database.User;
        _settings["Database:Password"] = database.Password;
        _settings["Database:Encrypt"] = database.Encrypt ? "true" : "false";
        return this;
    }

    public ApiFactory WithRedis(string connectionString)
    {
        _settings["Redis:ConnectionString"] = connectionString;
        _useRealRedis = true;
        return this;
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment(_environment);
        foreach (var (key, value) in _settings)
        {
            builder.UseSetting(key, value);
        }

        builder.ConfigureTestServices(services =>
        {
            services.AddSingleton<ILogEventSink>(LogSink);
            if (!_useRealRedis)
            {
                services.RemoveAll<ITokenVersionStore>();
                services.AddSingleton<ITokenVersionStore>(TokenVersions);
            }

            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);
        });
    }
}
