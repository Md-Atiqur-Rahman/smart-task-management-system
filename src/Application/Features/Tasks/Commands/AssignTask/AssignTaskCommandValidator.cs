using FluentValidation;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.AssignTask;

public sealed class AssignTaskCommandValidator : AbstractValidator<AssignTaskCommand>
{
    public AssignTaskCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.AssigneeId).NotEqual(Guid.Empty).When(c => c.AssigneeId is not null);
    }
}
