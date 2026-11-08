// SAMPLE: copy-source, delete or rename
using FluentValidation;

namespace Contoso.TemplateApp.Application.Features.Samples.Commands.DeleteSample;

public sealed class DeleteSampleCommandValidator : AbstractValidator<DeleteSampleCommand>
{
    public DeleteSampleCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}
