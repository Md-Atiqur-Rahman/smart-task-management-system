using System.Collections;
using System.Reflection;
using System.Text.Json.Nodes;

using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace Datavanced.SmartTaskManagement.Api.OpenApi;

/// <summary>
/// Makes the OpenAPI document identical on every operating system: every string
/// in the document gets "\r\n" and then a lone "\r" replaced by "\n".
/// Why: XML doc comments carry the platform newline into descriptions, so a
/// multi-line summary produced "\r\n" on Windows and "\n" on Linux, and the
/// committed docs/openapi.json failed the freshness check in CI.
/// Runs as the LAST document transformer. Uses the official
/// IOpenApiDocumentTransformer API:
/// https://learn.microsoft.com/en-us/aspnet/core/fundamentals/openapi/customize-openapi?view=aspnetcore-10.0
/// </summary>
internal sealed class LineEndingNormalizationTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(
        OpenApiDocument document,
        OpenApiDocumentTransformerContext context,
        CancellationToken cancellationToken)
    {
        new Walker().Visit(document);
        return Task.CompletedTask;
    }

    internal static string Normalize(string value) =>
        value.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');

    /// <summary>
    /// Visits the OpenAPI object graph: writable string properties, strings in
    /// dictionaries and lists, and JSON values (examples, defaults, enums).
    /// Reference objects are skipped; their targets are visited where they are
    /// defined (components). A reference-identity set prevents cycles.
    /// </summary>
    private sealed class Walker
    {
        private const string OpenApiNamespace = "Microsoft.OpenApi";

        private readonly HashSet<object> _visited = new(ReferenceEqualityComparer.Instance);

        public void Visit(object? node)
        {
            if (node is null or string || node.GetType().IsValueType || !_visited.Add(node))
            {
                return;
            }

            switch (node)
            {
                case JsonNode json:
                    VisitJson(json);
                    break;
                case IDictionary dictionary:
                    VisitDictionary(dictionary);
                    break;
                case IList list:
                    VisitList(list);
                    break;
                case IEnumerable sequence when IsOpenApiType(node.GetType()) is false:
                    foreach (var item in sequence)
                    {
                        Visit(item);
                    }

                    break;
                default:
                    VisitObject(node);
                    break;
            }
        }

        private static bool IsOpenApiType(Type type) =>
            type.Namespace?.StartsWith(OpenApiNamespace, StringComparison.Ordinal) == true;

        private void VisitObject(object node)
        {
            var type = node.GetType();
            if (!IsOpenApiType(type) || type.Name.EndsWith("Reference", StringComparison.Ordinal))
            {
                return;
            }

            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                var value = property.GetValue(node);
                var writable = property.CanWrite && property.SetMethod?.IsPublic == true;
                switch (value)
                {
                    case string text when writable:
                        property.SetValue(node, Normalize(text));
                        break;

                    // A JSON value cannot change in place; replace it in its owner.
                    case JsonValue json when writable:
                        property.SetValue(node, NormalizeJson(json));
                        break;
                    case string or JsonValue:
                        break;
                    default:
                        Visit(value);
                        break;
                }
            }
        }

        private void VisitDictionary(IDictionary dictionary)
        {
            foreach (var key in dictionary.Keys.Cast<object>().ToList())
            {
                switch (dictionary[key])
                {
                    case string text:
                        dictionary[key] = Normalize(text);
                        break;
                    case JsonNode json:
                        dictionary[key] = NormalizeJson(json);
                        break;
                    default:
                        Visit(dictionary[key]);
                        break;
                }
            }
        }

        private void VisitList(IList list)
        {
            for (var i = 0; i < list.Count; i++)
            {
                switch (list[i])
                {
                    case string text when !list.IsReadOnly:
                        list[i] = Normalize(text);
                        break;
                    case JsonNode json when !list.IsReadOnly:
                        list[i] = NormalizeJson(json);
                        break;
                    default:
                        Visit(list[i]);
                        break;
                }
            }
        }

        private void VisitJson(JsonNode json)
        {
            switch (json)
            {
                case JsonObject obj:
                    foreach (var name in obj.Select(p => p.Key).ToList())
                    {
                        var current = obj[name];
                        var normalized = NormalizeJson(current);
                        if (!ReferenceEquals(normalized, current))
                        {
                            obj[name] = normalized;
                        }
                    }

                    break;
                case JsonArray array:
                    for (var i = 0; i < array.Count; i++)
                    {
                        // Re-assigning the same node to its own array throws
                        // ("The node already has a parent"): replace only changed nodes.
                        var current = array[i];
                        var normalized = NormalizeJson(current);
                        if (!ReferenceEquals(normalized, current))
                        {
                            array[i] = normalized;
                        }
                    }

                    break;
            }
        }

        /// <summary>A new node for a string that changed; otherwise the same node (children visited).</summary>
        private JsonNode? NormalizeJson(JsonNode? json)
        {
            if (json is JsonValue value && value.TryGetValue<string>(out var text))
            {
                var normalized = Normalize(text);
                return string.Equals(normalized, text, StringComparison.Ordinal)
                    ? json
                    : JsonValue.Create(normalized);
            }

            if (json is not null)
            {
                VisitJson(json);
            }

            return json;
        }
    }
}
