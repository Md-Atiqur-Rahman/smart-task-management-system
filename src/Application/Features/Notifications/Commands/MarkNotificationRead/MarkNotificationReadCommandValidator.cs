using FluentValidation;

namespace Datavanced.SmartTaskManagement.Application.Features.Notifications.Commands.MarkNotificationRead;

public sealed class MarkNotificationReadCommandValidator : AbstractValidator<MarkNotificationReadCommand>
{
    public MarkNotificationReadCommandValidator()
    {
        RuleFor(c => c.Id).NotEmpty();
    }
}
