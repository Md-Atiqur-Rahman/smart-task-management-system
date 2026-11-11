using System.ComponentModel.DataAnnotations;

namespace Datavanced.SmartTaskManagement.Infrastructure.Configuration.Ai;

/// <summary>AI settings, bound from the "Ai" section (design 5, ADR 0020).</summary>
public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Google Gemini settings (section "Ai:Gemini").</summary>
    public GeminiOptions Gemini { get; init; } = new();

    /// <summary>The longest one Gemini call may take; below the frontend's 30-second request timeout (PRD 10).</summary>
    [Range(5, 60)]
    public int TimeoutSeconds { get; init; } = 25;

    [Range(256, 8192)]
    public int MaxOutputTokens { get; init; } = 2048;
}
