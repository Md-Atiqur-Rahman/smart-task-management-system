using System.Net;
using System.Text.Json.Nodes;

using Datavanced.SmartTaskManagement.Application.Abstractions.Ai;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Infrastructure.Ai;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Ai;
using Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>
/// GeminiTextImprover against a fake HTTP handler (design 5 and 8). No test calls the real Gemini API.
/// </summary>
public sealed class GeminiTextImproverTests : IDisposable
{
    private const string ApiKey = "test-key-5f1c9e";
    private const string Description = "SECRET-TEXT-123 login page e bug ache";

    private static readonly string ValidAnswer = new JsonObject
    {
        ["headings"] = new JsonObject { ["goal"] = "Goal", ["steps"] = "Steps", ["doneWhen"] = "Done when", ["notes"] = "Notes" },
        ["goal"] = "Fix the login bug.",
        ["steps"] = new JsonArray("Reproduce it.", "Fix it."),
        ["doneWhen"] = new JsonArray("Login works."),
        ["notes"] = new JsonArray(),
    }.ToJsonString();

    private readonly FakeTimeProvider _time = new();
    private readonly CapturingLogger<GeminiTextImprover> _logger = new();
    private readonly List<HttpClient> _clients = [];

    public void Dispose()
    {
        foreach (var client in _clients)
        {
            client.Dispose();
        }
    }

    [Fact]
    public async Task Improve_NoKey_NotConfigured_NoHttpCall()
    {
        using var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, Success(ValidAnswer));

        var result = await Improver(handler, apiKey: null).ImproveDescriptionAsync(null, Description, CancellationToken.None);

        result.Error.ShouldBe(AiErrors.NotConfigured);
        handler.Requests.ShouldBeEmpty();
    }

    [Fact]
    public async Task Improve_Request_HasPathHeaderAndBody()
    {
        using var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, Success(ValidAnswer));

        await Improver(handler).ImproveDescriptionAsync("Login", Description, CancellationToken.None);

        var request = handler.Requests.ShouldHaveSingleItem();
        request.Method.ShouldBe(HttpMethod.Post);
        request.Uri!.ToString().ShouldBe(
            "https://generativelanguage.googleapis.com/v1beta/models/gemini-3.5-flash-lite:generateContent");
        request.Uri.Query.ShouldBeEmpty();
        request.Uri.ToString().ShouldNotContain(ApiKey);
        request.Headers["x-goog-api-key"].ShouldBe(ApiKey);

        var body = JsonNode.Parse(request.Body!)!;
        body["contents"]![0]!["role"]!.GetValue<string>().ShouldBe("user");
        body["contents"]![0]!["parts"]![0]!["text"]!.GetValue<string>()
            .ShouldBe(AiPrompts.BuildUserMessage("Login", Description));
        body["systemInstruction"]!["parts"]![0]!["text"]!.GetValue<string>().ShouldBe(AiPrompts.SystemInstruction);
        var config = body["generationConfig"]!;
        config["responseMimeType"]!.GetValue<string>().ShouldBe("application/json");
        config["maxOutputTokens"]!.GetValue<int>().ShouldBe(2048);
        config["responseJsonSchema"]!.ToJsonString().ShouldBe(GeminiAnswerSchema.Create().ToJsonString());
        config["thinkingConfig"]!["thinkingLevel"]!.GetValue<string>().ShouldBe("MINIMAL");
        config["temperature"].ShouldBeNull();
        config["topP"].ShouldBeNull();
        config["topK"].ShouldBeNull();
    }

    [Fact]
    public async Task Improve_Success_SkipsThoughtPartsAndRenders()
    {
        var half = ValidAnswer.Length / 2;
        var response = new JsonObject
        {
            ["candidates"] = new JsonArray(new JsonObject
            {
                ["content"] = new JsonObject
                {
                    ["role"] = "model",
                    ["parts"] = new JsonArray(
                        new JsonObject { ["text"] = "thinking about it", ["thought"] = true },
                        new JsonObject { ["text"] = ValidAnswer[..half] },
                        new JsonObject { ["text"] = ValidAnswer[half..] }),
                },
                ["finishReason"] = "STOP",
            }),
        }.ToJsonString();

        using var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, response);

        var result = await Improver(handler)
            .ImproveDescriptionAsync(null, Description, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Goal: Fix the login bug.\n\nSteps:\n- Reproduce it.\n- Fix it.\n\nDone when:\n- Login works.");
    }

    public static TheoryData<string> BlockedResponses => new()
    {
        """{ "promptFeedback": { "blockReason": "SAFETY" } }""",
        """{ "promptFeedback": { "blockReason": "OTHER" } }""",
        Success(ValidAnswer, "SAFETY"),
        Success(ValidAnswer, "BLOCKLIST"),
        Success(ValidAnswer, "PROHIBITED_CONTENT"),
        Success(ValidAnswer, "SPII"),
        Success(ValidAnswer, "RECITATION"),
    };

    [Theory]
    [MemberData(nameof(BlockedResponses))]
    public async Task Improve_Blocked_IsRejected(string responseJson)
    {
        using var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, responseJson);
        var result = await Improver(handler)
            .ImproveDescriptionAsync(null, Description, CancellationToken.None);

        result.Error.ShouldBe(AiErrors.Rejected);
    }

    public static TheoryData<string> BadAnswers => new()
    {
        Success(ValidAnswer, "MAX_TOKENS"),
        Success(ValidAnswer, "OTHER"),
        """{ "candidates": [] }""",
        """{}""",
        Success(""),
        Success("not the json object"),
        "this is not json",
    };

    [Theory]
    [MemberData(nameof(BadAnswers))]
    public async Task Improve_BadAnswer_IsUnavailable(string responseJson)
    {
        using var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, responseJson);
        var result = await Improver(handler)
            .ImproveDescriptionAsync(null, Description, CancellationToken.None);

        result.Error.ShouldBe(AiErrors.Unavailable);
    }

    [Theory]
    [InlineData(408)]
    [InlineData(429)]
    [InlineData(500)]
    [InlineData(503)]
    public async Task Improve_TransientStatus_IsUnavailable(int status)
    {
        using var handler = FakeHttpMessageHandler.Json((HttpStatusCode)status, "{}");
        var result = await Improver(handler)
            .ImproveDescriptionAsync(null, Description, CancellationToken.None);

        result.Error.ShouldBe(AiErrors.Unavailable);
        _logger.Entries.ShouldNotContain(e => e.Level == LogLevel.Error);
    }

    [Theory]
    [InlineData(400)]
    [InlineData(401)]
    [InlineData(403)]
    [InlineData(404)]
    public async Task Improve_ConfigStatus_IsUnavailableAndLogsErrorWithoutKey(int status)
    {
        using var handler = FakeHttpMessageHandler.Json((HttpStatusCode)status, """{ "error": { "message": "API key not valid" } }""");
        var result = await Improver(handler)
            .ImproveDescriptionAsync(null, Description, CancellationToken.None);

        result.Error.ShouldBe(AiErrors.Unavailable);
        _logger.Entries.ShouldContain(e => e.Level == LogLevel.Error);
        _logger.Entries.ShouldAllBe(e => !e.AllText.Contains(ApiKey));
    }

    [Fact]
    public async Task Improve_SlowerThanTimeout_IsUnavailable()
    {
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new FakeHttpMessageHandler(async (_, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("unreachable");
        });

        var call = Improver(handler).ImproveDescriptionAsync(null, Description, CancellationToken.None);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        _time.Advance(TimeSpan.FromSeconds(5));
        var result = await call.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);

        result.Error.ShouldBe(AiErrors.Unavailable);
    }

    [Fact]
    public async Task Improve_CallerCancels_ThrowsOperationCanceled()
    {
        using var caller = new CancellationTokenSource();
        var started = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        using var handler = new FakeHttpMessageHandler(async (_, ct) =>
        {
            started.SetResult();
            await Task.Delay(Timeout.InfiniteTimeSpan, ct);
            throw new InvalidOperationException("unreachable");
        });

        var call = Improver(handler).ImproveDescriptionAsync(null, Description, caller.Token);
        await started.Task.WaitAsync(TimeSpan.FromSeconds(10), TestContext.Current.CancellationToken);
        await caller.CancelAsync();

        Exception? thrown = null;
        try
        {
            await call;
        }
        catch (OperationCanceledException ex)
        {
            thrown = ex;
        }

        thrown.ShouldNotBeNull();
    }

    [Fact]
    public async Task Improve_NetworkError_IsUnavailable_KeyNotInLogs()
    {
        using var handler = new FakeHttpMessageHandler((_, _) =>
            throw new HttpRequestException("No such host is known."));

        var result = await Improver(handler).ImproveDescriptionAsync(null, Description, CancellationToken.None);

        result.Error.ShouldBe(AiErrors.Unavailable);
        _logger.Entries.ShouldNotBeEmpty();
        _logger.Entries.ShouldAllBe(e => !e.AllText.Contains(ApiKey));
    }

    [Fact]
    public async Task Improve_NeverLogsDescriptionOrAnswer()
    {
        using var handler = FakeHttpMessageHandler.Json(HttpStatusCode.OK, Success(ValidAnswer));

        var result = await Improver(handler).ImproveDescriptionAsync("Secret title", Description, CancellationToken.None);

        result.IsSuccess.ShouldBeTrue();
        _logger.Entries.ShouldNotBeEmpty();
        _logger.Entries.ShouldAllBe(e =>
            !e.AllText.Contains("SECRET-TEXT-123")
            && !e.AllText.Contains("Secret title")
            && !e.AllText.Contains("Fix the login bug")
            && !e.AllText.Contains(ApiKey));
    }

    private GeminiTextImprover Improver(FakeHttpMessageHandler handler, string? apiKey = ApiKey)
    {
        var options = new AiOptions { Gemini = new GeminiOptions { ApiKey = apiKey }, TimeoutSeconds = 5 };
        var client = new HttpClient(handler, disposeHandler: false)
        {
            BaseAddress = options.Gemini.BaseUrl,
            Timeout = Timeout.InfiniteTimeSpan,
        };
        _clients.Add(client);
        return new GeminiTextImprover(client, Options.Create(options), _time, _logger);
    }

    private static string Success(string text, string finishReason = "STOP") => new JsonObject
    {
        ["candidates"] = new JsonArray(new JsonObject
        {
            ["content"] = new JsonObject
            {
                ["role"] = "model",
                ["parts"] = new JsonArray(new JsonObject { ["text"] = text }),
            },
            ["finishReason"] = finishReason,
        }),
    }.ToJsonString();
}
