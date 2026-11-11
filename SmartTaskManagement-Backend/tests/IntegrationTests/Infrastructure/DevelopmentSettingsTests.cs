using System.Text.Json;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>Development settings that must match the local frontend (its dev server runs on HTTPS).</summary>
public sealed class DevelopmentSettingsTests
{
    private const string FrontendDevServer = "https://localhost:4200";

    [Fact]
    public void Email_AppBaseUrl_IsTheHttpsFrontendDevServer()
    {
        var path = Path.Combine(RepositoryFiles.Root(), "src", "Api", "appsettings.Development.json");
        using var settings = JsonDocument.Parse(File.ReadAllText(path));

        var appBaseUrl = settings.RootElement.GetProperty("Email").GetProperty("AppBaseUrl").GetString();

        appBaseUrl.ShouldBe(FrontendDevServer);
    }
}
