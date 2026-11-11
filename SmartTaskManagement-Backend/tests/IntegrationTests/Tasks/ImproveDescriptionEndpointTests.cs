using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.Application.Abstractions.Ai;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Users;
using Datavanced.SmartTaskManagement.IntegrationTests.Auth;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

using NSubstitute;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Tasks;

/// <summary>
/// POST /api/tasks/description/improve (design 3). A fake IAiTextImprover stands in for Gemini; the
/// no-key test uses the real GeminiTextImprover, which answers without any HTTP call.
/// </summary>
[Collection(AuthCollectionDefinition.Name)]
public sealed class ImproveDescriptionEndpointTests : IDisposable
{
    private const string ImprovePath = "/api/tasks/description/improve";

    private readonly SqlServerFixture _database;
    private readonly RedisFixture _redis;
    private readonly IAiTextImprover _improver = Substitute.For<IAiTextImprover>();
    private readonly AuthApi _api;
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    public ImproveDescriptionEndpointTests(SqlServerFixture database, RedisFixture redis)
    {
        _database = database;
        _redis = redis;
        _api = new AuthApi(database, redis.ConnectionString, factory => factory.WithTestServices(services =>
        {
            services.RemoveAll<IAiTextImprover>();
            services.AddSingleton(_improver);
        }));
        _improver.ImproveDescriptionAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Success("Goal: Fix the login bug."));
    }

    [Fact]
    public async Task Improve_ProjectManager_Returns200WithFakeText()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);

        using var response = await _api.SendAsync(
            HttpMethod.Post, ImprovePath, pm.Token, new { title = "Login", description = "login bug fix" }, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("description").GetString().ShouldBe("Goal: Fix the login bug.");
        await _improver.Received(1).ImproveDescriptionAsync("Login", "login bug fix", Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Improve_Admin_Returns200()
    {
        var admin = await _api.CreateUserAsync(Role.Admin, _ct);

        using var response = await _api.SendAsync(
            HttpMethod.Post, ImprovePath, admin.Token, new { description = "login bug fix" }, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Improve_TeamMember_Returns403()
    {
        var member = await _api.CreateUserAsync(Role.TeamMember, _ct);

        using var response = await _api.SendAsync(
            HttpMethod.Post, ImprovePath, member.Token, new { description = "login bug fix" }, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
        await _improver.DidNotReceiveWithAnyArgs().ImproveDescriptionAsync(default, default!, _ct);
    }

    [Fact]
    public async Task Improve_NoToken_Returns401()
    {
        using var response = await _api.Client.PostAsJsonAsync(
            AuthApi.Uri(ImprovePath), new { description = "login bug fix" }, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Improve_BlankDescription_Returns400Validation()
    {
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);

        using var response = await _api.SendAsync(
            HttpMethod.Post, ImprovePath, pm.Token, new { title = "Login", description = "   " }, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).ShouldBe("validation.failed");
        await _improver.DidNotReceiveWithAnyArgs().ImproveDescriptionAsync(default, default!, _ct);
    }

    [Fact]
    public async Task Improve_Rejected_Returns400AiRejected()
    {
        _improver.ImproveDescriptionAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<string>(AiErrors.Rejected));
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);

        using var response = await _api.SendAsync(
            HttpMethod.Post, ImprovePath, pm.Token, new { description = "login bug fix" }, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        (await ErrorCodeAsync(response)).ShouldBe("ai.rejected");
    }

    [Fact]
    public async Task Improve_Unavailable_Returns503()
    {
        _improver.ImproveDescriptionAsync(Arg.Any<string?>(), Arg.Any<string>(), Arg.Any<CancellationToken>())
            .Returns(Result.Failure<string>(AiErrors.Unavailable));
        var pm = await _api.CreateUserAsync(Role.ProjectManager, _ct);

        using var response = await _api.SendAsync(
            HttpMethod.Post, ImprovePath, pm.Token, new { description = "login bug fix" }, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await ErrorCodeAsync(response)).ShouldBe("ai.unavailable");
    }

    [Fact]
    public async Task Improve_NoApiKey_Returns503NotConfigured()
    {
        using var api = NoKeyApi();
        var pm = await api.CreateUserAsync(Role.ProjectManager, _ct);

        using var response = await api.SendAsync(
            HttpMethod.Post, ImprovePath, pm.Token, new { description = "login bug fix" }, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await ErrorCodeAsync(response)).ShouldBe("ai.not_configured");
    }

    [Fact]
    public async Task Host_StartsWithoutApiKey_HealthIsHealthy()
    {
        using var api = NoKeyApi();

        using var response = await api.Client.GetAsync(AuthApi.Uri("/health"), _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    public void Dispose() => _api.Dispose();

    /// <summary>The real host with an explicitly empty key, so an environment variable cannot leak in.</summary>
    private AuthApi NoKeyApi() =>
        new(_database, _redis.ConnectionString, factory => factory.WithSetting("Ai:Gemini:ApiKey", string.Empty));

    private async Task<string?> ErrorCodeAsync(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<JsonElement>(_ct)).GetProperty("errorCode").GetString();
}
