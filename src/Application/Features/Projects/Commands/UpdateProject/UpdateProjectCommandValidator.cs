using Datavanced.SmartTaskManagement.Domain.Projects;

using FluentValidation;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.UpdateProject;

public sealed class UpdateProjectCommandValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
        RuleFor(c => c.Name).NotEmpty().Must(n => n.Trim().Length <= Project.NameMaxLength)
            .WithMessage("Name must be at most " + Project.NameMaxLength + " characters.");
        RuleFor(c => c.Description).Must(d => d is null || d.Trim().Length <= Project.DescriptionMaxLength)
            .WithMessage("Description must be at most " + Project.DescriptionMaxLength + " characters.");
    }
}
