using System.Text.Json.Nodes;

namespace Datavanced.SmartTaskManagement.Infrastructure.Ai;

/// <summary>
/// The generateContent request body (Google API reference, checked 2026-11-11): one user turn, the system
/// instruction and the generation settings. Serialized with camelCase names; null members are omitted.
/// </summary>
public sealed record GeminiRequest(
    IReadOnlyList<GeminiContent> Contents,
    GeminiContent SystemInstruction,
    GeminiGenerationConfig GenerationConfig);

/// <summary>A Content: a role ("user" or "model"; none for the system instruction) and its parts.</summary>
public sealed record GeminiContent(string? Role, IReadOnlyList<GeminiPart>? Parts);

/// <summary>A Part: its text, and on answers whether it is the model's thinking (never shown).</summary>
public sealed record GeminiPart(string? Text, bool? Thought = null);

public sealed record GeminiGenerationConfig(
    int MaxOutputTokens,
    string ResponseMimeType,
    JsonObject ResponseJsonSchema,
    GeminiThinkingConfig ThinkingConfig);

public sealed record GeminiThinkingConfig(string ThinkingLevel);
