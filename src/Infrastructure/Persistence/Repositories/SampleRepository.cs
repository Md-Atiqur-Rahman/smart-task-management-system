// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Domain.Samples;

using Microsoft.EntityFrameworkCore;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.Repositories;

public sealed class SampleRepository(AppDbContext context) : ISampleRepository
{
    public Task<Sample?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        context.Set<Sample>().SingleOrDefaultAsync(s => s.Id == id, cancellationToken);

    public void Add(Sample sample) => context.Set<Sample>().Add(sample);
}
