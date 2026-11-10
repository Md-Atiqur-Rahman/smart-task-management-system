using System.Net.Http.Json;

using Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

using Serilog.Events;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api;

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
        using var client = await AuthenticatedClient.ForNewTeamMemberAsync(factory, ct);

        using var response = await client.PostAsJsonAsync(
            new Uri("/api/samples", UriKind.Relative), new { name = "logging" }, ct);
        response.EnsureSuccessStatusCode();

        var events = factory.LogSink.Events;
        events.ShouldNotContain(e => e.Level <= LogEventLevel.Information && Source(e).StartsWith("Microsoft.EntityFrameworkCore", StringComparison.Ordinal));
        events.ShouldNotContain(e => e.Level <= LogEventLevel.Information && Source(e).StartsWith("Microsoft.AspNetCore", StringComparison.Ordinal));
        events.ShouldContain(e => e.MessageTemplate.Text.StartsWith("Handled {RequestName}", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LogEvents_CarryInstanceId()
    {
        var ct = TestContext.Current.CancellationToken;
        using var factory = new ApiFactory().WithSetting("Instance:Id", "test-1");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(new Uri("/test/anonymous", UriKind.Relative), ct);
        response.EnsureSuccessStatusCode();

        var events = factory.LogSink.Events;
        events.ShouldNotBeEmpty();
        events.ShouldAllBe(e => e.Properties.ContainsKey("InstanceId")
            && e.Properties["InstanceId"].ToString() == "\"test-1\"");
    }

    private static string Source(LogEvent logEvent) =>
        logEvent.Properties.TryGetValue("SourceContext", out var value)
            ? value.ToString().Trim('"')
            : string.Empty;
}
