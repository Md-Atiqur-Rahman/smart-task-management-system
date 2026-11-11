namespace Datavanced.SmartTaskManagement.Infrastructure.Ai;

/// <summary>
/// The parts of a generateContent response this feature reads (Google API reference, checked 2026-11-11).
/// Every member is nullable: the response is external input and is checked before use.
/// </summary>
public sealed record GeminiResponse(IReadOnlyList<GeminiCandidate?>? Candidates, GeminiPromptFeedback? PromptFeedback);

public sealed record GeminiCandidate(GeminiContent? Content, string? FinishReason);

/// <summary>Set when the prompt itself was blocked; then no candidates are returned.</summary>
public sealed record GeminiPromptFeedback(string? BlockReason);
