using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api;

/// <summary>
/// OpenAPI document and Swagger UI: served in Development only, with the JWT
/// Bearer security scheme (Authorize button). Development runs the dev-only
/// migration, so it uses the real SQL Server fixture.
/// </summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class OpenApiTests(SqlServerFixture database)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Development_ServesOpenApiDocument_WithBearerSchemeAndSamplePaths()
    {
        using var factory = new ApiFactory().WithEnvironment("Development").WithDatabase(database.Options);
        using var client = factory.CreateClient();

        var document = await client.GetFromJsonAsync<JsonElement>(Relative("/openapi/v1.json"), _ct);

        document.GetProperty("openapi").GetString().ShouldStartWith("3.1");
        var bearer = document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer");
        bearer.GetProperty("type").GetString().ShouldBe("http");
        bearer.GetProperty("scheme").GetString().ShouldBe("bearer");
        var paths = document.GetProperty("paths");
        paths.TryGetProperty("/api/samples", out _).ShouldBeTrue();
        paths.TryGetProperty("/api/samples/{id}", out _).ShouldBeTrue();
        paths.TryGetProperty("/api/samples/{id}/complete", out _).ShouldBeTrue();
    }

    // XML doc comments would otherwise carry the platform newline ("\r\n" on Windows).
    [Fact]
    public async Task Development_OpenApiDocument_HasNoCarriageReturns()
    {
        using var factory = new ApiFactory().WithEnvironment("Development").WithDatabase(database.Options);
        using var client = factory.CreateClient();

        var text = await client.GetStringAsync(Relative("/openapi/v1.json"), _ct);

        text.ShouldNotContain("\\r", Case.Sensitive);
        text.ShouldContain("a missing\\nvalue reaches FluentValidation", Case.Sensitive);
    }

    [Fact]
    public async Task Development_ServesSwaggerUi()
    {
        using var factory = new ApiFactory().WithEnvironment("Development").WithDatabase(database.Options);
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Relative("/swagger/index.html"), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync(_ct)).ShouldContain("swagger-ui", Case.Insensitive);
    }

    [Theory]
    [InlineData("/swagger/index.html")]
    [InlineData("/openapi/v1.json")]
    public async Task Production_DoesNotServeSwaggerOrOpenApi(string path)
    {
        using var factory = new ApiFactory()
            .WithEnvironment("Production")
            .WithDatabase(database.Options)
            .WithSetting("Database:Encrypt", "true")
            .WithRedis("localhost:6379,password=not-used-without-redis,ssl=true");
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Relative(path), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    private static Uri Relative(string path) => new(path, UriKind.Relative);
}
