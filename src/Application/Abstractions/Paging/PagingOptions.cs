using System.ComponentModel.DataAnnotations;

namespace Contoso.TemplateApp.Application.Abstractions.Paging;

/// <summary>Paging limits, bound from the "Paging" configuration section and validated on start.</summary>
public sealed class PagingOptions
{
    public const string SectionName = "Paging";

    [Range(1, 1000)]
    public int DefaultPageSize { get; init; } = 20;

    [Range(1, 1000)]
    public int MaxPageSize { get; init; } = 100;
}
