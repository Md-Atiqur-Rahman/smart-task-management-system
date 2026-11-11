using Datavanced.SmartTaskManagement.Application.Abstractions.Ai;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.ImproveTaskDescription;

internal sealed class ImproveTaskDescriptionCommandHandler(IAiTextImprover improver)
    : ICommandHandler<ImproveTaskDescriptionCommand, ImprovedDescriptionDto>
{
    public async Task<Result<ImprovedDescriptionDto>> Handle(
        ImproveTaskDescriptionCommand command,
        CancellationToken cancellationToken)
    {
        var title = string.IsNullOrWhiteSpace(command.Title) ? null : command.Title.Trim();

        var result = await improver.ImproveDescriptionAsync(title, command.Description.Trim(), cancellationToken);

        return result.IsSuccess
            ? Result.Success(new ImprovedDescriptionDto(result.Value))
            : Result.Failure<ImprovedDescriptionDto>(result.Error);
    }
}
