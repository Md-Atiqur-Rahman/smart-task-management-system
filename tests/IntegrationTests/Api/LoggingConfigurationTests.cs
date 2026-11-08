using System.Net.Http.Json;

using Contoso.TemplateApp.IntegrationTests.Infrastructure;

using Serilog.Events;

namespace Contoso.TemplateApp.IntegrationTests.Api;

/// <summary>
/// The Serilog minimum levels in appsettings.json take effect: noisy framework
/// categories (EF Core SQL commands, ASP.NET Core internals) are not logged at
/// Information, while application events are.
/// </summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class LoggingConfigurationTests(SqlServerFixture database)
{
    [Fact]
    public async Task FrameworkInformationLogs_AreFiltered_ApplicationLogsAreKept()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new ApiFactory().WithDatabase(database.Options);
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/samples", UriKind.Relative), new { name = "logging" }, ct);
        response.EnsureSuccessStatusCode();

        var events = factory.LogSink.Events;
        events.ShouldNotContain(e => e.Level <= LogEventLevel.Information && Source(e).StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        events.ShouldNotContain(e => e.Level <= LogEventLevel.Information && Source(e).StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        events.ShouldContain(e => e.MessageTemplate.Text.StartsWith("Handled {RequestName}", StringComparison.Ordinal));
    }

    private static string Source(LogEvent logEvent) =>
        logEvent.Properties.TryGetValue("SourceContext", out var value)
            ? value.ToString().Trim('"')
            : string.Empty;
}
