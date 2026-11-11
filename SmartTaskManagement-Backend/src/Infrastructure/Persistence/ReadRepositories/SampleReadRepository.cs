// SAMPLE: copy-source, delete or rename
using System.Linq.Expressions;

using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Features.Samples;
using Datavanced.SmartTaskManagement.Application.Features.Samples.Queries.ListSamples;
using Datavanced.SmartTaskManagement.Domain.Samples;
using Datavanced.SmartTaskManagement.Infrastructure.Persistence.Querying;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.ReadRepositories;

/// <summary>AsNoTracking + hand-written Select projection straight to SampleDto.</summary>
public sealed class SampleReadRepository(AppDbContext context) : ISampleReadRepository
{
    private static readonly Expression<Func<Sample, SampleDto>> ToDto = s =>
        new SampleDto(s.Id, s.Name, s.Description, s.Status, s.CreatedAt, s.UpdatedAt);

    // The whitelist: SampleSortFields key -> expression. Nothing else can be sorted on.
    private static readonly Dictionary<string, Expression<Func<Sample, object?>>> SortMap =
        new(StringComparer.OrdinalIgnoreCase)
        {
            [SampleSortFields.Name] = s => s.Name,
            [SampleSortFields.CreatedAt] = s => s.CreatedAt,
            [SampleSortFields.Status] = s => s.Status,
        };

    public Task<SampleDto?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<Sample>()
            .AsNoTracking()
            .Where(s => s.Id == id)
            .Select(ToDto)
            .SingleOrDefaultAsync(cancellationToken);

    public Task<PagedResult<SampleDto>> ListAsync(ListSamplesQuery query, CancellationToken cancellationToken) =>
        context.Set<Sample>()
            .ApplySearch(query.Search, s => s.Name, s => s.Description)
            .ApplySort(query.SortBy, query.SortDirection, SortMap, SampleSortFields.CreatedAt, s => s.Id)
            .ToPagedResultAsync(ToDto, query.Page, query.PageSize, cancellationToken);
}
