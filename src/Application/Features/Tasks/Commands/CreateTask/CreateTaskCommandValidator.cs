using Datavanced.SmartTaskManagement.Domain.Tasks;

using FluentValidation;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.CreateTask;

public sealed class CreateTaskCommandValidator : AbstractValidator<CreateTaskCommand>
{
    public CreateTaskCommandValidator()
    {
        RuleFor(c => c.ProjectId).NotEmpty();
        RuleFor(c => c.Title).Cascade(CascadeMode.Stop).NotEmpty()
            .Must(t => t.Trim().Length <= TaskItem.TitleMaxLength)
            .WithMessage("Title must be at most " + TaskItem.TitleMaxLength + " characters.");
        RuleFor(c => c.Description).Must(d => d is null || d.Trim().Length <= TaskItem.DescriptionMaxLength)
            .WithMessage("Description must be at most " + TaskItem.DescriptionMaxLength + " characters.");
        RuleFor(c => c.Priority).IsInEnum();
        RuleFor(c => c.AssigneeId).NotEqual(Guid.Empty).When(c => c.AssigneeId is not null);
    }
}
