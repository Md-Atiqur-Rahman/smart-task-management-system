// SAMPLE: copy-source, delete or rename
using FluentValidation;

namespace Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.DeleteSample;

public sealed class DeleteSampleCommandValidator : AbstractValidator<DeleteSampleCommand>
{
    public DeleteSampleCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}
