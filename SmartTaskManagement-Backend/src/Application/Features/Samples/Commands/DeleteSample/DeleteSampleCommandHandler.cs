// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Samples;

namespace Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.DeleteSample;

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
