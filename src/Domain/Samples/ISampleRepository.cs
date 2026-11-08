// SAMPLE: copy-source, delete or rename
namespace Contoso.TemplateApp.Domain.Samples;

/// <summary>Write repository of the Sample aggregate. Changes are saved by IUnitOfWork.</summary>
public interface ISampleRepository
{
    /// <summary>Tracked aggregate, or null when it does not exist or is soft-deleted.</summary>
    Task<Sample?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    void Add(Sample sample);
}
