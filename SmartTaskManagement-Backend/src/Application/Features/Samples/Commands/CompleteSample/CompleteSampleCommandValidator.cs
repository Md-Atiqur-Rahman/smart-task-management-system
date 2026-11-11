// SAMPLE: copy-source, delete or rename
using FluentValidation;

namespace Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.CompleteSample;

public sealed class CompleteSampleCommandValidator : AbstractValidator<CompleteSampleCommand>
{
    public CompleteSampleCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}
