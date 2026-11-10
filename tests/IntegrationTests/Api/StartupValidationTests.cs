using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api;

/// <summary>Review Focus 5: missing or invalid secrets fail at startup, not at the first request.</summary>
public sealed class StartupValidationTests
{
    [Theory]
    [InlineData("Jwt:SigningKey", "")]
    [InlineData("Jwt:SigningKey", "too-short")]
    [InlineData("Database:Password", "")]
    [InlineData("Database:Name", "")]
    public void MissingOrInvalidSecret_HostFailsToStart_WithOptionsValidationMessage(
        string key, string value)
    {
        using var factory = new ApiFactory().WithSetting(key, value);

        var exception = Should.Throw<Exception>(() => factory.CreateClient());

        var validation = FindValidationException(exception);
        validation.ShouldNotBeNull($"Expected an OptionsValidationException, got: {exception}");
        validation.Message.ShouldContain(key.Split(':')[1]);
    }

    [Fact]
    public void Startup_PartialAdminSeedSettings_FailsValidation()
    {
        using var factory = new ApiFactory().WithSetting("AdminSeed:Email", "admin@example.com");

        var exception = Should.Throw<Exception>(() => factory.CreateClient());

        var validation = FindValidationException(exception);
        validation.ShouldNotBeNull($"Expected an OptionsValidationException, got: {exception}");
        validation.Message.ShouldContain("AdminSeed");
    }

    [Fact]
    public void Startup_AdminSeedPasswordBreaksPolicy_FailsValidation()
    {
        using var factory = new ApiFactory()
            .WithSetting("AdminSeed:Email", "admin@example.com")
            .WithSetting("AdminSeed:Password", "short1")
            .WithSetting("AdminSeed:FullName", "Admin");

        var exception = Should.Throw<Exception>(() => factory.CreateClient());

        var validation = FindValidationException(exception);
        validation.ShouldNotBeNull($"Expected an OptionsValidationException, got: {exception}");
        validation.Message.ShouldContain("AdminSeed:Password");
    }

    [Fact]
    public void Startup_NoAdminSeedSettings_Starts()
    {
        using var factory = new ApiFactory();

        Should.NotThrow(() => factory.CreateClient());
    }

    [Theory]
    [InlineData("*")]
    [InlineData("https://app.example.test/")]
    [InlineData("https://app.example.test/app")]
    [InlineData("https://app.example.test?x=1")]
    [InlineData("https://app.example.test#top")]
    [InlineData("ftp://app.example.test")]
    [InlineData("app.example.test")]
    [InlineData("")]
    public void Startup_InvalidCorsOrigin_FailsValidation(string origin)
    {
        using var factory = new ApiFactory().WithSetting("Cors:AllowedOrigins:0", origin);

        var exception = Should.Throw<Exception>(() => factory.CreateClient());

        var validation = FindValidationException(exception);
        validation.ShouldNotBeNull($"Expected an OptionsValidationException, got: {exception}");
        validation.Message.ShouldContain("Cors:AllowedOrigins");
    }

    [Theory]
    [InlineData("https://app.example.test")]
    [InlineData("http://localhost:4200")]
    public void Startup_ValidCorsOrigin_Starts(string origin)
    {
        using var factory = new ApiFactory().WithSetting("Cors:AllowedOrigins:0", origin);

        Should.NotThrow(() => factory.CreateClient());
    }

    private static OptionsValidationException? FindValidationException(Exception? exception)
    {
        while (exception is not null)
        {
            if (exception is OptionsValidationException validation)
            {
                return validation;
            }

            if (exception is AggregateException aggregate)
            {
                return aggregate.InnerExceptions
                    .Select(FindValidationException)
                    .FirstOrDefault(e => e is not null);
            }

            exception = exception.InnerException;
        }

        return null;
    }
}
