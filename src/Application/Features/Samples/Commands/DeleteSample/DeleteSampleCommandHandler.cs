// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Application.Abstractions.Messaging;
using Contoso.TemplateApp.Domain.Abstractions;
using Contoso.TemplateApp.Domain.Results;
using Contoso.TemplateApp.Domain.Samples;

namespace Contoso.TemplateApp.Application.Features.Samples.Commands.DeleteSample;

/// <summary>Soft delete; the time comes from TimeProvider, never DateTime.UtcNow.</summary>
public sealed class DeleteSampleCommandHandler(
    ISampleRepository repository,
    IUnitOfWork unitOfWork,
    TimeProvider timeProvider)
    : ICommandHandler<DeleteSampleCommand>
{
    public async Task<Result> Handle(DeleteSampleCommand command, CancellationToken cancellationToken)
    {
        var sample = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (sample is null)
        {
            return Result.Failure(SampleErrors.NotFound);
        }

        sample.Delete(timeProvider.GetUtcNow());
        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
