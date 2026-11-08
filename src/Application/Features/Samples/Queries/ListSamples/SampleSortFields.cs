// SAMPLE: copy-source, delete or rename
namespace Contoso.TemplateApp.Application.Features.Samples.Queries.ListSamples;

/// <summary>
/// The sort WHITELIST: the only values SortBy may take. Infrastructure maps each
/// key to an expression; a user string never reaches OrderBy.
/// </summary>
public static class SampleSortFields
{
    public const string Name = "name";
    public const string CreatedAt = "createdAt";
    public const string Status = "status";

    public static readonly IReadOnlyCollection<string> All = [Name, CreatedAt, Status];
}
