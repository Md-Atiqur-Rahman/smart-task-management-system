// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Domain.Samples;

using FluentValidation;

namespace Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.CreateSample;

public sealed class CreateSampleCommandValidator : AbstractValidator<CreateSampleCommand>
{
    public CreateSampleCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Sample.NameMaxLength);
        RuleFor(c => c.Description).MaximumLength(Sample.DescriptionMaxLength);
    }
}
