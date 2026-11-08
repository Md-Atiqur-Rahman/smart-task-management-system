using System.Text.Json;
using System.Text.Json.Nodes;

using Datavanced.SmartTaskManagement.Api.OpenApi;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api;

/// <summary>
/// The OpenAPI document must be byte-identical on Windows and Linux. XML doc
/// comments carry the platform newline into descriptions, so every string is
/// normalized to "\n". The carriage returns are built in code here: a source file
/// would be normalized to LF by git and prove nothing.
/// </summary>
public sealed class OpenApiLineEndingTests
{
    private const string Windows = "first line\r\nsecond line";
    private const string OldMac = "first line\rsecond line";

    [Fact]
    public async Task CarriageReturns_InEveryKindOfString_AreNormalized()
    {
        var document = DocumentWith(Windows, OldMac);

        await Transform(document);

        var json = await Serialize(document);
        var remaining = json.Split('\n').Where(line => line.Contains("\\r", StringComparison.Ordinal)).ToList();
        remaining.ShouldBeEmpty("Strings still containing \\r: " + string.Join(" | ", remaining));
        StringValues(JsonNode.Parse(json)!).ShouldAllBe(s => !s.Contains('\r', StringComparison.Ordinal));
        json.ShouldContain("first line\\nsecond line");
    }

    [Fact]
    public async Task Document_WithoutCarriageReturns_IsUnchanged()
    {
        var document = DocumentWith("first line\nsecond line", "single line");
        var before = await Serialize(document);

        await Transform(document);

        (await Serialize(document)).ShouldBe(before);
    }

    private static OpenApiDocument DocumentWith(string multiLine, string other)
    {
        var nested = new OpenApiSchema { Type = JsonSchemaType.String, Description = multiLine, Default = JsonValue.Create(multiLine) };

        // Arrays with non-string elements must survive (a node cannot be re-added to its own parent).
        var arrayExample = new OpenApiSchema
        {
            Type = JsonSchemaType.Array,
            Default = new JsonArray(1, true, new JsonObject { ["k"] = multiLine }, new JsonArray(2, multiLine), multiLine),
        };
        var schema = new OpenApiSchema
        {
            Type = JsonSchemaType.Object,
            Title = other,
            Description = multiLine,
            Properties = new Dictionary<string, IOpenApiSchema> { ["name"] = nested, ["values"] = arrayExample },
            Enum = [JsonValue.Create(other)!],
        };

        var operation = new OpenApiOperation
        {
            Summary = other,
            Description = multiLine,
            Parameters =
            [
                new OpenApiParameter { Name = "search", In = ParameterLocation.Query, Description = multiLine },
            ],
            Responses = new OpenApiResponses
            {
                ["200"] = new OpenApiResponse { Description = multiLine },
            },
        };

        return new OpenApiDocument
        {
            Info = new OpenApiInfo { Title = "probe", Version = "1", Description = multiLine, Summary = other },
            Paths = new OpenApiPaths
            {
                ["/probe"] = new OpenApiPathItem
                {
                    Description = multiLine,
                    Operations = new Dictionary<HttpMethod, OpenApiOperation> { [HttpMethod.Get] = operation },
                },
            },
            Components = new OpenApiComponents
            {
                Schemas = new Dictionary<string, IOpenApiSchema> { ["Probe"] = schema },
            },
            Tags = new HashSet<OpenApiTag> { new() { Name = "probe", Description = multiLine } },
        };
    }

    private static Task Transform(OpenApiDocument document)
    {
        using var services = new ServiceCollection().BuildServiceProvider();
        var context = new OpenApiDocumentTransformerContext
        {
            DocumentName = "v1",
            DescriptionGroups = [],
            ApplicationServices = services,
        };
        return new LineEndingNormalizationTransformer().TransformAsync(document, context, CancellationToken.None);
    }

    private static async Task<string> Serialize(OpenApiDocument document)
    {
        await using var writer = new StringWriter();
        document.SerializeAsV31(new OpenApiJsonWriter(writer));
        return writer.ToString();
    }

    private static IEnumerable<string> StringValues(JsonNode node) => node switch
    {
        JsonObject obj => obj.SelectMany(p => p.Value is null ? [] : StringValues(p.Value)),
        JsonArray array => array.SelectMany(item => item is null ? [] : StringValues(item)),
        JsonValue value when value.GetValueKind() == JsonValueKind.String => [value.GetValue<string>()],
        _ => [],
    };
}
