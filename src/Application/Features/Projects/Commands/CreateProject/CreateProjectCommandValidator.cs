using Datavanced.SmartTaskManagement.Domain.Projects;

using FluentValidation;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.CreateProject;

public sealed class CreateProjectCommandValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectCommandValidator()
    {
        RuleFor(c => c.Name).NotEmpty().Must(n => n.Trim().Length <= Project.NameMaxLength)
            .WithMessage("Name must be at most " + Project.NameMaxLength + " characters.");
        RuleFor(c => c.Description).Must(d => d is null || d.Trim().Length <= Project.DescriptionMaxLength)
            .WithMessage("Description must be at most " + Project.DescriptionMaxLength + " characters.");
        RuleFor(c => c.OwnerId).NotEqual(Guid.Empty).When(c => c.OwnerId is not null);
    }
}
