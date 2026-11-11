using System.Text.Json.Nodes;

namespace Datavanced.SmartTaskManagement.Infrastructure.Ai;

/// <summary>
/// The JSON schema sent as generationConfig.responseJsonSchema. It mirrors <see cref="GeminiAnswer"/> and
/// the checks in <see cref="GeminiAnswerRenderer"/>; change all three together.
/// </summary>
public static class GeminiAnswerSchema
{
    public static JsonObject Create() => new()
    {
        ["type"] = "object",
        ["properties"] = new JsonObject
        {
            ["headings"] = new JsonObject
            {
                ["type"] = "object",
                ["properties"] = new JsonObject
                {
                    ["goal"] = Text(),
                    ["steps"] = Text(),
                    ["doneWhen"] = Text(),
                    ["notes"] = Text(),
                },
                ["required"] = Names("goal", "steps", "doneWhen", "notes"),
            },
            ["goal"] = Text(),
            ["steps"] = TextList(GeminiAnswer.MinSteps, GeminiAnswer.MaxSteps),
            ["doneWhen"] = TextList(GeminiAnswer.MinDoneWhen, GeminiAnswer.MaxDoneWhen),
            ["notes"] = new JsonObject { ["type"] = "array", ["items"] = Text() },
        },
        ["required"] = Names("headings", "goal", "steps", "doneWhen", "notes"),
    };

    private static JsonObject Text() => new() { ["type"] = "string" };

    private static JsonObject TextList(int minItems, int maxItems) => new()
    {
        ["type"] = "array",
        ["items"] = Text(),
        ["minItems"] = minItems,
        ["maxItems"] = maxItems,
    };

    private static JsonArray Names(params string[] names) =>
        new(names.Select(n => (JsonNode?)JsonValue.Create(n)).ToArray());
}
