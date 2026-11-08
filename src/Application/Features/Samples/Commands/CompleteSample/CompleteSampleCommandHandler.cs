// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Samples;

namespace Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.CompleteSample;

/// <summary>Write path with a Result-returning domain state change.</summary>
public sealed class CompleteSampleCommandHandler(ISampleRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<CompleteSampleCommand>
{
    public async Task<Result> Handle(CompleteSampleCommand command, CancellationToken cancellationToken)
    {
        var sample = await repository.GetByIdAsync(command.Id, cancellationToken);
        if (sample is null)
        {
            return Result.Failure(SampleErrors.NotFound);
        }

        var completed = sample.Complete();
        if (completed.IsFailure)
        {
            return completed;
        }

        return await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
