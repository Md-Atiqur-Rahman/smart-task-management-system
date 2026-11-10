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
    public void Startup_NoAdminSeedSettings_Starts()
    {
        using var factory = new ApiFactory();

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
