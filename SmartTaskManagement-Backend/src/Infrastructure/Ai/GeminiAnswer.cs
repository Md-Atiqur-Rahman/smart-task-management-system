namespace Datavanced.SmartTaskManagement.Infrastructure.Ai;

/// <summary>
/// The JSON object the model is asked to return (design 4). Every member is nullable because the model's
/// output is untrusted: <see cref="GeminiAnswerRenderer"/> checks it before anything is rendered.
/// </summary>
public sealed record GeminiAnswer(
    GeminiAnswerHeadings? Headings,
    string? Goal,
    IReadOnlyList<string?>? Steps,
    IReadOnlyList<string?>? DoneWhen,
    IReadOnlyList<string?>? Notes)
{
    public const int MinSteps = 2;
    public const int MaxSteps = 7;
    public const int MinDoneWhen = 1;
    public const int MaxDoneWhen = 4;
}

/// <summary>The section headings, written by the model in the description's language.</summary>
public sealed record GeminiAnswerHeadings(string? Goal, string? Steps, string? DoneWhen, string? Notes);
