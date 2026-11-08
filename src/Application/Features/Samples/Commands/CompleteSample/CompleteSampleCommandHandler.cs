// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Application.Abstractions.Messaging;
using Contoso.TemplateApp.Domain.Abstractions;
using Contoso.TemplateApp.Domain.Results;
using Contoso.TemplateApp.Domain.Samples;

namespace Contoso.TemplateApp.Application.Features.Samples.Commands.CompleteSample;

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
