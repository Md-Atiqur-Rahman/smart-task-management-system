// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Domain.Abstractions;
using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Samples;

namespace Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.CreateSample;

/// <summary>Write path: aggregate factory -> write repository -> UnitOfWork.</summary>
public sealed class CreateSampleCommandHandler(ISampleRepository repository, IUnitOfWork unitOfWork)
    : ICommandHandler<CreateSampleCommand, Guid>
{
    public async Task<Result<Guid>> Handle(CreateSampleCommand command, CancellationToken cancellationToken)
    {
        var sample = Sample.Create(command.Name, command.Description);
        repository.Add(sample);

        var saved = await unitOfWork.SaveChangesAsync(cancellationToken);
        return saved.IsSuccess ? Result.Success(sample.Id) : Result.Failure<Guid>(saved.Error);
    }
}
