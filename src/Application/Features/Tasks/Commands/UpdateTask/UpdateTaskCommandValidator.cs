using Datavanced.SmartTaskManagement.Domain.Tasks;

using FluentValidation;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.UpdateTask;

public sealed class UpdateTaskCommandValidator : AbstractValidator<UpdateTaskCommand>
{
    public UpdateTaskCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Title).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(t => t.Trim().Length <= TaskItem.TitleMaxLength)
            .WithMessage("Title must be at most " + TaskItem.TitleMaxLength + " characters.");
        RuleFor(c => c.Description).Must(d => d is null || d.Trim().Length <= TaskItem.DescriptionMaxLength)
            .WithMessage("Description must be at most " + TaskItem.DescriptionMaxLength + " characters.");
        RuleFor(c => c.Priority).IsInEnum();
    }
}
