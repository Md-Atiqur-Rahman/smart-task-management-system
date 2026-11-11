using System.ComponentModel.DataAnnotations;

using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Email;

using Microsoft.Extensions.Hosting.Internal;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>Email:UseTls=false only in Development and Testing (like ADR 0011); link templates need {id}.</summary>
public sealed class EmailOptionsTests
{
    [Fact]
    public void Options_UseTlsFalseInProduction_FailsValidation()
    {
        var result = Validate("Production", Valid(useTls: false));

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("UseTls");
    }

    [Theory]
    [InlineData("Development")]
    [InlineData("Testing")]
    public void Options_UseTlsFalseInDevelopmentOrTesting_IsValid(string environment)
    {
        Validate(environment, Valid(useTls: false)).Succeeded.ShouldBeTrue();
    }

    [Fact]
    public void Options_UseTlsTrueInProduction_IsValid()
    {
        Validate("Production", Valid()).Succeeded.ShouldBeTrue();
    }

    [Theory]
    [InlineData("/tasks/", "/projects/{id}", "TaskPathTemplate")]
    [InlineData("/tasks/{id}", "/projects", "ProjectPathTemplate")]
    public void Options_TemplateWithoutId_FailsValidation(string taskTemplate, string projectTemplate, string expected)
    {
        var result = Validate("Production", Valid(taskPathTemplate: taskTemplate, projectPathTemplate: projectTemplate));

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain(expected);
    }

    [Fact]
    public void Options_UserNameWithoutPassword_FailsValidation()
    {
        var result = Validate("Production", Valid(userName: "mailer"));

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("Password");
    }

    [Fact]
    public void Options_RelativeAppBaseUrl_FailsValidation()
    {
        var result = Validate("Production", Valid(appBaseUrl: "localhost:4200"));

        result.Failed.ShouldBeTrue();
        result.FailureMessage.ShouldContain("AppBaseUrl");
    }

    [Fact]
    public void Defaults_MatchTheDesign()
    {
        var defaults = new EmailOptions();

        defaults.UseTls.ShouldBeTrue();
        defaults.TaskPathTemplate.ShouldBe("/tasks/{id}");
        defaults.ProjectPathTemplate.ShouldBe("/projects/{id}");
        defaults.PollSeconds.ShouldBe(10);
        defaults.BatchSize.ShouldBe(20);
        defaults.ClaimSeconds.ShouldBe(120);
        defaults.MaxAttempts.ShouldBe(5);
    }

    [Fact]
    public void DataAnnotations_RangesFromTheDesign()
    {
        Annotate(Valid(pollSeconds: 0)).ShouldContain(nameof(EmailOptions.PollSeconds));
        Annotate(Valid(pollSeconds: 301)).ShouldContain(nameof(EmailOptions.PollSeconds));
        Annotate(Valid(batchSize: 101)).ShouldContain(nameof(EmailOptions.BatchSize));
        Annotate(Valid(claimSeconds: 29)).ShouldContain(nameof(EmailOptions.ClaimSeconds));
        Annotate(Valid(claimSeconds: 901)).ShouldContain(nameof(EmailOptions.ClaimSeconds));
        Annotate(Valid(maxAttempts: 21)).ShouldContain(nameof(EmailOptions.MaxAttempts));
        Annotate(Valid(host: "")).ShouldContain(nameof(EmailOptions.Host));
        Annotate(Valid(fromAddress: "not-an-address")).ShouldContain(nameof(EmailOptions.FromAddress));
        Annotate(Valid()).ShouldBeEmpty();
    }

    private static EmailOptions Valid(
        string host = "smtp.example.com",
        bool useTls = true,
        string? userName = null,
        string fromAddress = "notifications@example.com",
        string appBaseUrl = "https://app.example.com",
        string taskPathTemplate = "/tasks/{id}",
        string projectPathTemplate = "/projects/{id}",
        int pollSeconds = 10,
        int batchSize = 20,
        int claimSeconds = 120,
        int maxAttempts = 5) => new()
        {
            Host = host,
            Port = 587,
            UseTls = useTls,
            UserName = userName,
            FromAddress = fromAddress,
            FromName = "Task notifications",
            AppBaseUrl = new Uri(appBaseUrl, UriKind.RelativeOrAbsolute),
            TaskPathTemplate = taskPathTemplate,
            ProjectPathTemplate = projectPathTemplate,
            PollSeconds = pollSeconds,
            BatchSize = batchSize,
            ClaimSeconds = claimSeconds,
            MaxAttempts = maxAttempts,
        };

    private static ValidateOptionsResult Validate(string environment, EmailOptions options) =>
        new EmailOptionsValidator(new HostingEnvironment { EnvironmentName = environment }).Validate(null, options);

    private static List<string> Annotate(EmailOptions options)
    {
        var results = new List<ValidationResult>();
        Validator.TryValidateObject(options, new ValidationContext(options), results, validateAllProperties: true);
        return results.SelectMany(r => r.MemberNames).ToList();
    }
}
