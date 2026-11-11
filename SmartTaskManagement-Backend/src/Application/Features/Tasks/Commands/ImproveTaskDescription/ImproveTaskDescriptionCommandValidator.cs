using Datavanced.SmartTaskManagement.Domain.Tasks;

using FluentValidation;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.ImproveTaskDescription;

public sealed class ImproveTaskDescriptionCommandValidator : AbstractValidator<ImproveTaskDescriptionCommand>
{
    public ImproveTaskDescriptionCommandValidator()
    {
        RuleFor(c => c.Description).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(d => d.Trim().Length <= TaskItem.DescriptionMaxLength)
            .WithMessage("Description must be at most " + TaskItem.DescriptionMaxLength + " characters.");
        RuleFor(c => c.Title).Must(t => t is null || t.Trim().Length <= TaskItem.TitleMaxLength)
            .WithMessage("Title must be at most " + TaskItem.TitleMaxLength + " characters.");
    }
}
