// SAMPLE: copy-source, delete or rename
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.CompleteSample;
using Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.DeleteSample;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Samples;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence;
using Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using static Datavanced.SmartTaskManagement.IntegrationTests.Samples.SampleApi;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Samples;

/// <summary>POST, complete and DELETE through HTTP against real SQL Server.</summary>
[Collection(SqlServerCollectionDefinition.Name)]
public sealed class SampleWriteTests(SqlServerFixture database) : IAsyncLifetime, IDisposable
{
    private readonly SampleApi _api = new(database);
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Create_Valid_Returns201WithLocationAndId()
    {
        using var response = await _api.Client.PostAsJsonAsync(
            Uri(Route), new { name = "first", description = "desc" }, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.Created);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        var id = body.GetProperty("id").GetGuid();
        response.Headers.Location.ShouldNotBeNull().OriginalString.ShouldEndWith($"{Route}/{id}");
    }

    [Fact]
    public async Task Create_EmptyName_Returns400WithErrorsMap()
    {
        using var response = await _api.Client.PostAsJsonAsync(Uri(Route), new { name = "" }, _ct);

        await ShouldBeValidationProblem(response, "Name");
    }

    [Fact]
    public async Task Create_MissingName_Returns400WithErrorsMap()
    {
        using var response = await _api.Client.PostAsJsonAsync(Uri(Route), new { description = "x" }, _ct);

        await ShouldBeValidationProblem(response, "Name");
    }

    [Fact]
    public async Task Create_MalformedJson_Returns400ValidationProblem()
    {
        using var content = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        using var response = await _api.Client.PostAsync(Uri(Route), content, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("errorCode").GetString().ShouldBe(ValidationError.ErrorCode);
    }

    [Fact]
    public async Task CompleteTwice_Returns204Then409()
    {
        var id = await _api.CreateAsync("complete me", _ct);

        using var first = await _api.Client.PostAsync(Uri($"{Route}/{id}/complete"), null, _ct);
        using var second = await _api.Client.PostAsync(Uri($"{Route}/{id}/complete"), null, _ct);

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.Conflict);
        var body = await second.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("errorCode").GetString().ShouldBe("sample.already_completed");
    }

    [Fact]
    public async Task Complete_Unknown_Returns404()
    {
        using var response = await _api.Client.PostAsync(Uri($"{Route}/{Guid.NewGuid()}/complete"), null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("errorCode").GetString().ShouldBe("sample.not_found");
    }

    [Fact]
    public async Task DeleteTwice_Returns204Then404()
    {
        var id = await _api.CreateAsync("delete me", _ct);

        using var first = await _api.Client.DeleteAsync(Uri($"{Route}/{id}"), _ct);
        using var second = await _api.Client.DeleteAsync(Uri($"{Route}/{id}"), _ct);

        first.StatusCode.ShouldBe(HttpStatusCode.NoContent);
        second.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Complete_DeletedSample_Returns404()
    {
        var id = await _api.CreateAsync("deleted then completed", _ct);
        (await _api.Client.DeleteAsync(Uri($"{Route}/{id}"), _ct)).StatusCode.ShouldBe(HttpStatusCode.NoContent);

        using var response = await _api.Client.PostAsync(Uri($"{Route}/{id}/complete"), null, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("POST", "/api/samples/not-a-guid/complete")]
    [InlineData("DELETE", "/api/samples/not-a-guid")]
    public async Task NonGuidId_Returns404(string method, string path)
    {
        using var request = new HttpRequestMessage(new HttpMethod(method), Uri(path));

        using var response = await _api.Client.SendAsync(request, _ct);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    // Review Focus 1: another request changed the row after this one loaded it.
    [Fact]
    public async Task Delete_WithStaleRowVersion_ReturnsConflict()
    {
        var id = await _api.CreateAsync("stale delete", _ct);
        using var scope = _api.Factory.Services.CreateScope();
        await PreloadAsync(scope, id);

        (await _api.Client.PostAsync(Uri($"{Route}/{id}/complete"), null, _ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<DeleteSampleCommand>>();
        var result = await handler.Handle(new DeleteSampleCommand(id), _ct);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe(UnitOfWork.ConcurrencyConflictCode);
    }

    [Fact]
    public async Task Complete_WithStaleRowVersion_ReturnsConflict()
    {
        var id = await _api.CreateAsync("stale complete", _ct);
        using var scope = _api.Factory.Services.CreateScope();
        await PreloadAsync(scope, id);

        // Another request completes it first; this scope still holds the stale Open copy.
        (await _api.Client.PostAsync(Uri($"{Route}/{id}/complete"), null, _ct))
            .StatusCode.ShouldBe(HttpStatusCode.NoContent);

        var handler = scope.ServiceProvider.GetRequiredService<ICommandHandler<CompleteSampleCommand>>();
        var result = await handler.Handle(new CompleteSampleCommand(id), _ct);

        result.Error.Type.ShouldBe(ErrorType.Conflict);
        result.Error.Code.ShouldBe(UnitOfWork.ConcurrencyConflictCode);
    }

    public ValueTask InitializeAsync() => new(_api.SignInAsync(_ct));

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    public void Dispose() => _api.Dispose();

    // Tracks the current row (and its RowVersion) in this scope's DbContext; the
    // handler resolved from the same scope then works on that stale instance.
    private async Task PreloadAsync(IServiceScope scope, Guid id)
    {
        var context = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        (await context.Set<Sample>().SingleAsync(s => s.Id == id, _ct)).ShouldNotBeNull();
    }

    private async Task ShouldBeValidationProblem(HttpResponseMessage response, string field)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var body = await response.Content.ReadFromJsonAsync<JsonElement>(_ct);
        body.GetProperty("errorCode").GetString().ShouldBe(ValidationError.ErrorCode);
        body.GetProperty("errors").TryGetProperty(field, out _).ShouldBeTrue();
    }
}
