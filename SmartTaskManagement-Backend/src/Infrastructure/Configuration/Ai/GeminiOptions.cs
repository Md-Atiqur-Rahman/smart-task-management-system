namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Ai;

/// <summary>
/// Google Gemini API settings ("Ai:Gemini"). <see cref="ApiKey"/> is a secret: user-secrets or the
/// Ai__Gemini__ApiKey environment variable only, never an appsettings file. It is optional: without it
/// the improve endpoint returns ai.not_configured and the rest of the API works.
/// </summary>
public sealed class GeminiOptions
{
    public const string DefaultModel = "gemini-3.5-flash-lite";

    public static readonly Uri DefaultBaseUrl = new("https://generativelanguage.googleapis.com/");

    /// <summary>Secret, optional.</summary>
    public string? ApiKey { get; init; }

    /// <summary>The model id; a newer model is a configuration change.</summary>
    public string Model { get; init; } = DefaultModel;

    /// <summary>Absolute https base URL of the Gemini API.</summary>
    public Uri BaseUrl { get; init; } = DefaultBaseUrl;

    public bool IsConfigured => !string.IsNullOrWhiteSpace(ApiKey);
}
