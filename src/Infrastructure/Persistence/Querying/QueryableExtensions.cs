using System.Linq.Expressions;

using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.Querying;

/// <summary>
/// Reusable search / sort / paging for every read repository. Sorting accepts
/// only whitelisted keys mapped to expressions; the search term is sent to SQL
/// as a parameter (never inlined), and EF Core escapes LIKE wildcards in it.
/// </summary>
public static class QueryableExtensions
{
    /// <summary>Rows where ANY of the given text fields contains the term (no-op when blank).</summary>
    public static IQueryable<T> ApplySearch<T>(
        this IQueryable<T> query,
        string? term,
        params Expression<Func<T, string?>>[] fields)
    {
        if (string.IsNullOrWhiteSpace(term) || fields.Length == 0)
        {
            return query;
        }

        var parameter = Expression.Parameter(typeof(T), "e");

        // Captured through a closure object so EF Core sends it as a SQL parameter.
        var holder = new SearchTerm(term.Trim());
        var termExpression = Expression.Property(Expression.Constant(holder), nameof(SearchTerm.Value));
        var contains = typeof(string).GetMethod(nameof(string.Contains), [typeof(string)])!;

        Expression? any = null;
        foreach (var field in fields)
        {
            var body = new ParameterReplacer(field.Parameters[0], parameter).Visit(field.Body);
            var match = Expression.AndAlso(
                Expression.NotEqual(body, Expression.Constant(null, typeof(string))),
                Expression.Call(body, contains, termExpression));
            any = any is null ? match : Expression.OrElse(any, match);
        }

        return query.Where(Expression.Lambda<Func<T, bool>>(any!, parameter));
    }

    /// <summary>
    /// Orders by a WHITELISTED key (case-insensitive), falling back to
    /// <paramref name="defaultKey"/>; <paramref name="tieBreaker"/> makes paging stable.
    /// </summary>
    public static IQueryable<T> ApplySort<T>(
        this IQueryable<T> query,
        string? sortBy,
        SortDirection direction,
        IReadOnlyDictionary<string, Expression<Func<T, object?>>> whitelist,
        string defaultKey,
        Expression<Func<T, object?>> tieBreaker)
    {
        var key = sortBy is not null && whitelist.ContainsKey(sortBy) ? sortBy : defaultKey;
        var ordered = direction == SortDirection.Descending
            ? query.OrderByDescending(whitelist[key])
            : query.OrderBy(whitelist[key]);

        return direction == SortDirection.Descending
            ? ordered.ThenByDescending(tieBreaker)
            : ordered.ThenBy(tieBreaker);
    }

    /// <summary>Counts, takes one page and projects it to DTOs, without tracking.</summary>
    public static async Task<PagedResult<TDto>> ToPagedResultAsync<T, TDto>(
        this IQueryable<T> query,
        Expression<Func<T, TDto>> projection,
        int page,
        int pageSize,
        CancellationToken cancellationToken)
        where T : class
    {
        var source = query.AsNoTracking();
        var totalCount = await source.CountAsync(cancellationToken);
        var items = await source
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(projection)
            .ToListAsync(cancellationToken);

        return new PagedResult<TDto>(items, page, pageSize, totalCount);
    }

    private sealed record SearchTerm(string Value);

    private sealed class ParameterReplacer(ParameterExpression from, ParameterExpression to) : ExpressionVisitor
    {
        protected override Expression VisitParameter(ParameterExpression node) =>
            node == from ? to : base.VisitParameter(node);
    }
}
