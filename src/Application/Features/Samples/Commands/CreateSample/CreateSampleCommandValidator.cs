// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Domain.Samples;

using FluentValidation;

namespace Contoso.TemplateApp.Application.Features.Samples.Commands.CreateSample;

public sealed class CreateSampleCommandValidator : AbstractValidator<CreateSampleCommand>
{
    public CreateSampleCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().MaximumLength(Sample.NameMaxLength);
        RuleFor(c => c.Description).MaximumLength(Sample.DescriptionMaxLength);
    }
}
