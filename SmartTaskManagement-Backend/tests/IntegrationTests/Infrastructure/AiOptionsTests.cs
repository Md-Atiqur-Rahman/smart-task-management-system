using System.ComponentModel.DataAnnotations;

using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Ai;

using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>Ai settings (design 5): the key is optional, everything else has a validated default.</summary>
public sealed class AiOptionsTests
{
    [Fact]
    public void Options_Defaults_AreValid_WithoutApiKey()
    {
        var defaults = new AiOptions();

        defaults.Gemini.ApiKey.ShouldBeNull();
        defaults.Gemini.IsConfigured.ShouldBeFalse();
        defaults.Gemini.Model.ShouldBe("gemini-3.5-flash-lite");
        defaults.Gemini.BaseUrl.ShouldBe(new Uri("https://generativelanguage.googleapis.com/"));
        defaults.TimeoutSeconds.ShouldBe(25);
        defaults.MaxOutputTokens.ShouldBe(2048);
        Annotate(defaults).ShouldBeEmpty();
        Validate(defaults).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Options_WithApiKey_IsConfigured()
    {
        new GeminiOptions { ApiKey = "test-key" }.IsConfigured.ShouldBeTrue();
        new GeminiOptions { ApiKey = "   " }.IsConfigured.ShouldBeFalse();
    }

    [Theory]
    [InlineData(4)]
    [InlineData(61)]
    public void Options_TimeoutOutOfRange_Fails(int seconds)
    {
        Annotate(new AiOptions { TimeoutSeconds = seconds }).ShouldContain(nameof(AiOptions.TimeoutSeconds));
    }

    [Theory]
    [InlineData(255)]
    [InlineData(8193)]
    public void Options_MaxOutputTokensOutOfRange_Fails(int tokens)
    {
        Annotate(new AiOptions { MaxOutputTokens = tokens }).ShouldContain(nameof(AiOptions.MaxOutputTokens));
    }

    [Theory]
    [InlineData("http://generativelanguage.googleapis.com/")]
    [InlineData("generativelanguage.googleapis.com")]
    public void Options_NonHttpsBaseUrl_Fails(string address)
    {
        var options = new AiOptions
        {
            Gemini = new GeminiOptions { BaseUrl = new Uri(address, UriKind.RelativeOrAbsolute) },
        };

        var result = Validate(options);

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("BaseUrl");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Options_BlankModel_Fails(string model)
    {
        var result = Validate(new AiOptions { Gemini = new GeminiOptions { Model = model } });

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Model");
    }

    private static ValidateOptionsResult Validate(AiOptions options) =>
        new AiOptionsValidator().Validate(null, options);

    private static List<string> Annotate(AiOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results.SelectMany(r => r.MemberNames).ToList();
    }
}
