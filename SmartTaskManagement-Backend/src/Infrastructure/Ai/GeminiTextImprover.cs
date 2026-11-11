using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;

using Datavanced.SmartTaskManagement.Application.Abstractions.Ai;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Infrastructure.Configuration.Ai;

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Infrastructure.Ai;

/// <summary>
/// Improves a task description with the Google Gemini generateContent REST endpoint (design 5, ADR 0020).
/// The key travels only in the x-goog-api-key header. Every failure is a Result error: no key ->
/// ai.not_configured; blocked prompt or answer -> ai.rejected; anything else -> ai.unavailable. No retry.
/// Logs carry status, finish reason, input length and duration, never the text, the answer or the key.
/// </summary>
public sealed partial class GeminiTextImprover(
    HttpClient httpClient,
    IOptions<AiOptions> options,
    TimeProvider timeProvider,
    ILogger<GeminiTextImprover> logger) : IAiTextImprover
{
    public const string ApiKeyHeader = "x-goog-api-key";
    public const string ThinkingLevel = "MINIMAL";

    private const string UserRole = "user";
    private const string JsonMimeType = "application/json";
    private const string StopReason = "STOP";

    /// <summary>Finish and block reasons that mean Gemini refused the text (design 5).</summary>
    private static readonly HashSet<string> RejectedFinishReasons =
        ["SAFETY", "BLOCKLIST", "PROHIBITED_CONTENT", "SPII", "RECITATION"];

    /// <summary>Statuses that point at a configuration problem (bad key, model id or project).</summary>
    private static readonly HashSet<HttpStatusCode> ConfigurationStatuses =
        [HttpStatusCode.BadRequest, HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden, HttpStatusCode.NotFound];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<Result<string>> ImproveDescriptionAsync(
        string? title,
        string description,
        CancellationToken cancellationToken)
    {
        var settings = options.Value;
        if (!settings.Gemini.IsConfigured)
        {
            return Result.Failure<string>(AiErrors.NotConfigured);
        }

        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(settings.TimeoutSeconds), timeProvider);
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
        var started = timeProvider.GetTimestamp();

        using var request = new HttpRequestMessage(HttpMethod.Post, RequestPath(settings.Gemini.Model))
        {
            Content = JsonContent.Create(BuildRequest(title, description, settings), options: JsonOptions),
        };
        request.Headers.Add(ApiKeyHeader, settings.Gemini.ApiKey);

        try
        {
            using var response = await httpClient.SendAsync(request, linked.Token);
            if (!response.IsSuccessStatusCode)
            {
                LogFailedStatus(response.StatusCode, description.Length, Elapsed(started));
                return Result.Failure<string>(AiErrors.Unavailable);
            }

            var body = await response.Content.ReadAsStringAsync(linked.Token);
            return Interpret(body, description.Length, Elapsed(started));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogTimedOut(settings.TimeoutSeconds, description.Length);
            return Result.Failure<string>(AiErrors.Unavailable);
        }
        catch (HttpRequestException ex)
        {
            // Only the type and the socket error, never the message: the call carries the key in a header.
            LogNetworkError(ex.GetType().Name, ex.HttpRequestError, description.Length, Elapsed(started));
            return Result.Failure<string>(AiErrors.Unavailable);
        }
    }

    private static string RequestPath(string model) =>
        $"v1beta/models/{Uri.EscapeDataString(model)}:generateContent";

    private static GeminiRequest BuildRequest(string? title, string description, AiOptions settings) => new(
        Contents: [new GeminiContent(UserRole, [new GeminiPart(AiPrompts.BuildUserMessage(title, description))])],
        SystemInstruction: new GeminiContent(null, [new GeminiPart(AiPrompts.SystemInstruction)]),
        GenerationConfig: new GeminiGenerationConfig(
            settings.MaxOutputTokens,
            JsonMimeType,
            GeminiAnswerSchema.Create(),
            new GeminiThinkingConfig(ThinkingLevel)));

    private Result<string> Interpret(string body, int inputLength, long elapsedMs)
    {
        GeminiResponse? response;
        try
        {
            response = JsonSerializer.Deserialize<GeminiResponse>(body, JsonOptions);
        }
        catch (JsonException)
        {
            LogUnusableAnswer("unparsable response", inputLength, elapsedMs);
            return Result.Failure<string>(AiErrors.Unavailable);
        }

        if (response?.PromptFeedback?.BlockReason is { Length: > 0 } blockReason)
        {
            LogRejected(blockReason, inputLength, elapsedMs);
            return Result.Failure<string>(AiErrors.Rejected);
        }

        var candidate = response?.Candidates is { Count: > 0 } candidates ? candidates[0] : null;
        var finishReason = candidate?.FinishReason;
        if (finishReason is not null && RejectedFinishReasons.Contains(finishReason))
        {
            LogRejected(finishReason, inputLength, elapsedMs);
            return Result.Failure<string>(AiErrors.Rejected);
        }

        // Only a natural stop is a complete answer; MAX_TOKENS and every other reason are not (design 5).
        if (candidate is null || (finishReason is not null && finishReason != StopReason))
        {
            LogUnusableAnswer(finishReason ?? "no candidate", inputLength, elapsedMs);
            return Result.Failure<string>(AiErrors.Unavailable);
        }

        var text = string.Concat(
            (candidate.Content?.Parts ?? []).Where(p => p.Thought != true).Select(p => p.Text));
        var rendered = GeminiAnswerRenderer.Render(text);
        if (rendered.IsFailure)
        {
            LogUnusableAnswer("answer failed the checks", inputLength, elapsedMs);
            return rendered;
        }

        LogAnswered(finishReason ?? StopReason, inputLength, elapsedMs);
        return rendered;
    }

    private long Elapsed(long started) => (long)timeProvider.GetElapsedTime(started).TotalMilliseconds;

    private void LogFailedStatus(HttpStatusCode status, int inputLength, long elapsedMs)
    {
        if (ConfigurationStatuses.Contains(status))
        {
            LogConfigurationProblem((int)status, inputLength, elapsedMs);
        }
        else
        {
            LogUnavailableStatus((int)status, inputLength, elapsedMs);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Gemini improved a description: finish reason {FinishReason}, input length {InputLength}, {ElapsedMs} ms")]
    private partial void LogAnswered(string finishReason, int inputLength, long elapsedMs);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Gemini refused the text: reason {Reason}, input length {InputLength}, {ElapsedMs} ms")]
    private partial void LogRejected(string reason, int inputLength, long elapsedMs);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Gemini answer was not usable ({Reason}): input length {InputLength}, {ElapsedMs} ms")]
    private partial void LogUnusableAnswer(string reason, int inputLength, long elapsedMs);

    [LoggerMessage(
        Level = LogLevel.Error,
        Message = "Gemini returned status {StatusCode}, a configuration problem (check Ai:Gemini:ApiKey and Ai:Gemini:Model): input length {InputLength}, {ElapsedMs} ms")]
    private partial void LogConfigurationProblem(int statusCode, int inputLength, long elapsedMs);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Gemini returned status {StatusCode}: input length {InputLength}, {ElapsedMs} ms")]
    private partial void LogUnavailableStatus(int statusCode, int inputLength, long elapsedMs);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Gemini did not answer within {TimeoutSeconds} s: input length {InputLength}")]
    private partial void LogTimedOut(int timeoutSeconds, int inputLength);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Gemini call failed with {ExceptionType} ({RequestError}): input length {InputLength}, {ElapsedMs} ms")]
    private partial void LogNetworkError(string exceptionType, HttpRequestError requestError, int inputLength, long elapsedMs);
}
