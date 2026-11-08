using FluentValidation;

using Microsoft.Extensions.Options;

namespace Contoso.TemplateApp.Application.Abstractions.Paging;

/// <summary>
/// Shared rules for every list query: page and page-size limits from Options,
/// a defined sort direction, and SortBy only from the whitelist.
/// </summary>
public abstract class ListQueryValidator<TQuery> : AbstractValidator<TQuery>
    where TQuery : ListQuery
{
    public const int MaxSearchLength = 200;

    protected ListQueryValidator(
        IOptions<PagingOptions> options,
        IReadOnlyCollection<string> allowedSortFields)
    {
        var maxPageSize = options.Value.MaxPageSize;

        // Upper bound keeps the row offset ((page - 1) * pageSize) inside int range;
        // a larger page would overflow and reach SQL as a negative OFFSET (500).
        RuleFor(q => q.Page).InclusiveBetween(1, int.MaxValue / maxPageSize);
        RuleFor(q => q.PageSize).InclusiveBetween(1, maxPageSize);
        RuleFor(q => q.SortDirection).IsInEnum();
        RuleFor(q => q.Search).MaximumLength(MaxSearchLength);
        RuleFor(q => q.SortBy)
            .Must(sortBy => sortBy is null
                || allowedSortFields.Contains(sortBy, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"SortBy must be one of: {string.Join(", ", allowedSortFields)}.");
    }
}
