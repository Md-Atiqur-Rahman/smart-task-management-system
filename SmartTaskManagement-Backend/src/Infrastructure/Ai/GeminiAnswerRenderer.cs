using System.Text;
using System.Text.Json;

using Datavanced.SmartTaskManagement.Application.Abstractions.Ai;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Infrastructure.Ai;

/// <summary>
/// Checks the model's JSON answer and renders the plain-text description (design 4): goal line, steps,
/// done-when and, only when present, notes, each list item as a "- " line. Any failed check, or a text
/// longer than the task description limit, is ai.unavailable; nothing is ever cut.
/// </summary>
public static class GeminiAnswerRenderer
{
    private const string Bullet = "- ";
    private const char NewLine = '\n';

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static Result<string> Render(string json)
    {
        GeminiAnswer? answer;
        try
        {
            answer = JsonSerializer.Deserialize<GeminiAnswer>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return Result.Failure<string>(AiErrors.Unavailable);
        }

        if (answer?.Headings is not { } headings
            || !AllPresent(headings.Goal, headings.Steps, headings.DoneWhen, headings.Notes)
            || string.IsNullOrWhiteSpace(answer.Goal)
            || !IsValidList(answer.Steps, GeminiAnswer.MinSteps, GeminiAnswer.MaxSteps)
            || !IsValidList(answer.DoneWhen, GeminiAnswer.MinDoneWhen, GeminiAnswer.MaxDoneWhen)
            || !IsValidList(answer.Notes, 0, int.MaxValue))
        {
            return Result.Failure<string>(AiErrors.Unavailable);
        }

        var text = new StringBuilder()
            .Append(headings.Goal!.Trim()).Append(": ").Append(answer.Goal.Trim());
        AppendSection(text, headings.Steps!, answer.Steps!);
        AppendSection(text, headings.DoneWhen!, answer.DoneWhen!);
        if (answer.Notes is { Count: > 0 } notes)
        {
            AppendSection(text, headings.Notes!, notes);
        }

        return text.Length <= TaskItem.DescriptionMaxLength
            ? Result.Success(text.ToString())
            : Result.Failure<string>(AiErrors.Unavailable);
    }

    private static bool AllPresent(params string?[] values) =>
        values.All(v => !string.IsNullOrWhiteSpace(v));

    /// <summary>A missing notes list counts as empty; steps and done-when must be present.</summary>
    private static bool IsValidList(IReadOnlyList<string?>? items, int min, int max)
    {
        if (items is null)
        {
            return min == 0;
        }

        return items.Count >= min && items.Count <= max && items.All(i => !string.IsNullOrWhiteSpace(i));
    }

    private static void AppendSection(StringBuilder text, string heading, IReadOnlyList<string?> items)
    {
        text.Append(NewLine).Append(NewLine).Append(heading.Trim()).Append(':');
        foreach (var item in items)
        {
            text.Append(NewLine).Append(Bullet).Append(item!.Trim());
        }
    }
}
