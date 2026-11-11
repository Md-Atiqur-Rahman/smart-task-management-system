using System.Text.Json.Nodes;

using Datavanced.SmartTaskManagement.Application.Abstractions.Ai;
using Datavanced.SmartTaskManagement.Infrastructure.Ai;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Infrastructure;

/// <summary>The model's JSON answer is checked and rendered to the plain-text description (design 4).</summary>
public sealed class GeminiAnswerRendererTests
{
    private const string EnglishHeadings =
        """{ "goal": "Goal", "steps": "Steps", "doneWhen": "Done when", "notes": "Notes" }""";

    [Fact]
    public void Render_FullAnswer_ExactText()
    {
        var json = Answer(
            goal: "Fix the login error.",
            steps: ["Reproduce the error.", "Find the cause.", "Fix it."],
            doneWhen: ["Users can log in."],
            notes: ["Error text: \"Invalid token\"."]);

        var result = GeminiAnswerRenderer.Render(json);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(
            "Goal: Fix the login error.\n"
            + "\n"
            + "Steps:\n"
            + "- Reproduce the error.\n"
            + "- Find the cause.\n"
            + "- Fix it.\n"
            + "\n"
            + "Done when:\n"
            + "- Users can log in.\n"
            + "\n"
            + "Notes:\n"
            + "- Error text: \"Invalid token\".");
    }

    [Fact]
    public void Render_NoNotes_OmitsNotesSection()
    {
        var result = GeminiAnswerRenderer.Render(Answer(notes: []));

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotContain("Notes");
        result.Value.ShouldEndWith("- Users can log in.");
    }

    [Fact]
    public void Render_MissingNotes_OmitsNotesSection()
    {
        var node = JsonNode.Parse(Answer())!.AsObject();
        node.Remove("notes");

        var result = GeminiAnswerRenderer.Render(node.ToJsonString());

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldNotContain("Notes");
    }

    [Fact]
    public void Render_BanglishHeadings_UsedVerbatim()
    {
        var json = Answer(
            headings: """{ "goal": "Lokkho", "steps": "Dhap", "doneWhen": "Kaj shesh hobe jokhon", "notes": "Notes" }""",
            goal: "Login page er bug thik kora.",
            steps: ["Bug ta abar kore dekha.", "Thik kora."],
            doneWhen: ["Login kaj kore."],
            notes: []);

        var result = GeminiAnswerRenderer.Render(json);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe(
            "Lokkho: Login page er bug thik kora.\n\nDhap:\n- Bug ta abar kore dekha.\n- Thik kora.\n\n"
            + "Kaj shesh hobe jokhon:\n- Login kaj kore.");
    }

    [Fact]
    public void Render_TrimsItems()
    {
        var json = Answer(
            goal: "  Fix it.  ",
            steps: ["  One.", "Two.  "],
            doneWhen: ["  Done.  "],
            notes: [" Fact. "]);

        var result = GeminiAnswerRenderer.Render(json);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBe("Goal: Fix it.\n\nSteps:\n- One.\n- Two.\n\nDone when:\n- Done.\n\nNotes:\n- Fact.");
    }

    public static TheoryData<string> InvalidAnswers => new()
    {
        "not json",
        "[]",
        "{}",
        "null",
        Answer(goal: "  "),
        Answer(steps: ["Only one."]),
        Answer(steps: ["1", "2", "3", "4", "5", "6", "7", "8"]),
        Answer(doneWhen: []),
        Answer(doneWhen: ["1", "2", "3", "4", "5"]),
        Answer(steps: ["Step.", "  "]),
        Answer(notes: [" "]),
        Answer(headings: """{ "goal": "Goal", "steps": " ", "doneWhen": "Done when", "notes": "Notes" }"""),
        Answer(headings: """{ "goal": "Goal", "steps": "Steps", "doneWhen": "Done when" }"""),
        Answer(headings: "null"),
        """{ "headings": { "goal": "Goal", "steps": "Steps", "doneWhen": "Done when", "notes": "Notes" }, "goal": "G", "doneWhen": ["D"] }""",
        """{ "headings": { "goal": "Goal", "steps": "Steps", "doneWhen": "Done when", "notes": "Notes" }, "goal": 5, "steps": ["a", "b"], "doneWhen": ["D"] }""",
    };

    [Theory]
    [MemberData(nameof(InvalidAnswers))]
    public void Render_InvalidAnswer_IsUnavailable(string json)
    {
        var result = GeminiAnswerRenderer.Render(json);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AiErrors.Unavailable);
    }

    [Fact]
    public void Render_Over4000Chars_IsUnavailable()
    {
        var longStep = new string('x', 700);
        var json = Answer(steps: [longStep, longStep, longStep, longStep, longStep, longStep]);

        var result = GeminiAnswerRenderer.Render(json);

        result.IsFailure.ShouldBeTrue();
        result.Error.ShouldBe(AiErrors.Unavailable);
    }

    [Fact]
    public void Render_Exactly4000Chars_Succeeds()
    {
        var shortJson = Answer(steps: ["a", "b"]);
        var baseLength = GeminiAnswerRenderer.Render(shortJson).Value.Length;
        var json = Answer(steps: ["a", new string('b', 4000 - baseLength + 1)]);

        var result = GeminiAnswerRenderer.Render(json);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Length.ShouldBe(4000);
    }

    [Fact]
    public void Schema_RequiresTheRenderedFieldsAndLimits()
    {
        var schema = GeminiAnswerSchema.Create();

        schema["type"]!.GetValue<string>().ShouldBe("object");
        var required = schema["required"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
        required.ShouldBe(["headings", "goal", "steps", "doneWhen", "notes"], ignoreOrder: true);
        schema["properties"]!["steps"]!["minItems"]!.GetValue<int>().ShouldBe(2);
        schema["properties"]!["steps"]!["maxItems"]!.GetValue<int>().ShouldBe(7);
        schema["properties"]!["doneWhen"]!["minItems"]!.GetValue<int>().ShouldBe(1);
        schema["properties"]!["doneWhen"]!["maxItems"]!.GetValue<int>().ShouldBe(4);
        var headingKeys = schema["properties"]!["headings"]!["required"]!.AsArray()
            .Select(n => n!.GetValue<string>()).ToList();
        headingKeys.ShouldBe(["goal", "steps", "doneWhen", "notes"], ignoreOrder: true);
    }

    private static string Answer(
        string headings = EnglishHeadings,
        string goal = "Fix the login error.",
        string[]? steps = null,
        string[]? doneWhen = null,
        string[]? notes = null)
    {
        var node = new JsonObject
        {
            ["headings"] = JsonNode.Parse(headings),
            ["goal"] = goal,
            ["steps"] = ToArray(steps ?? ["Reproduce the error.", "Fix it."]),
            ["doneWhen"] = ToArray(doneWhen ?? ["Users can log in."]),
            ["notes"] = ToArray(notes ?? []),
        };
        return node.ToJsonString();
    }

    private static JsonArray ToArray(string[] items) => new(items.Select(i => (JsonNode?)JsonValue.Create(i)).ToArray());
}
