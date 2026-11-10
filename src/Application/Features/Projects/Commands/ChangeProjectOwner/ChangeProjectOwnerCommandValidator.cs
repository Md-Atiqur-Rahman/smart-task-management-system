using FluentValidation;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.ChangeProjectOwner;

public sealed class ChangeProjectOwnerCommandValidator : AbstractValidator<ChangeProjectOwnerCommand>
{
    public ChangeProjectOwnerCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.OwnerId).NotEmpty();
    }
}
