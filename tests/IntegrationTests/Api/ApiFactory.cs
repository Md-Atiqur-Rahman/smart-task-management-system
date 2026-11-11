using Datavanced.SmartTaskManagement.Application.Abstractions.Email;
using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Database;
using Datavanced.SmartTaskManagement.Infrastructure.Email;
using Datavanced.SmartTaskManagement.Infrastructure.LiveEvents;
using Datavanced.SmartTaskManagement.Infrastructure.Redis;
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

    private TimeProvider? _timeProvider;

    private Action<IServiceCollection>? _testServices;

    public CapturingSink LogSink { get; } = new();

    /// <summary>The in-memory token version store used unless <see cref="WithRedis"/> is called.</summary>
    public InMemoryTokenVersionStore TokenVersions { get; } = new();

    /// <summary>The in-memory rate limit counter used unless <see cref="WithRedis"/> is called.</summary>
    public InMemoryFixedWindowCounter RateLimitCounter { get; } = new();

    /// <summary>Records emails instead of sending them (the outbox worker does not run in tests).</summary>
    public FakeEmailSender Emails { get; } = new();

    /// <summary>Replaces the host's TimeProvider (for example a fixed clock).</summary>
    public ApiFactory WithTimeProvider(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        return this;
    }

    /// <summary>Extra service changes applied after every other test override (for example another live event publisher).</summary>
    public ApiFactory WithTestServices(Action<IServiceCollection> configure)
    {
        _testServices += configure;
        return this;
    }

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

            // No SMTP and no background email delivery: tests call EmailOutboxProcessor directly when needed.
            services.RemoveAll<IEmailSender>();
            services.AddSingleton<IEmailSender>(Emails);
            var emailWorker = services.FirstOrDefault(d => d.ImplementationType == typeof(EmailOutboxWorker));
            if (emailWorker is not null)
            {
                services.Remove(emailWorker);
            }
            if (!_useRealRedis)
            {
                services.RemoveAll<ITokenVersionStore>();
                services.AddSingleton<ITokenVersionStore>(TokenVersions);
                services.RemoveAll<IFixedWindowCounter>();
                services.AddSingleton<IFixedWindowCounter>(RateLimitCounter);

                // No Redis: deliver live events in process and do not subscribe to a Redis channel.
                services.RemoveAll<ILiveEventPublisher>();
                services.AddSingleton<InProcessLiveEventPublisher>(provider =>
                    new InProcessLiveEventPublisher(provider.GetRequiredService<LiveConnectionRegistry>()));
                services.AddSingleton<ILiveEventPublisher>(provider =>
                    provider.GetRequiredService<InProcessLiveEventPublisher>());
                var subscriber = services.FirstOrDefault(d => d.ImplementationType == typeof(RedisLiveEventSubscriber));
                if (subscriber is not null)
                {
                    services.Remove(subscriber);
                }
            }

            if (_timeProvider is not null)
            {
                services.RemoveAll<TimeProvider>();
                services.AddSingleton(_timeProvider);
            }

            services.AddControllers().AddApplicationPart(typeof(ApiFactory).Assembly);
            _testServices?.Invoke(services);
        });
    }
}
