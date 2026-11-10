using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Domain.Tasks;

/// <summary>Expected failures of the TaskItem aggregate. Codes are part of the API contract.</summary>
public static class TaskErrors
{
    public static readonly Error NotFound =
        Error.NotFound("tasks.not_found", "The task was not found.");

    public static readonly Error InvalidAssignee =
        Error.Validation("tasks.invalid_assignee", "The assignee must be an existing user.");

    public static readonly Error AssigneeNotAllowed =
        Error.Validation("tasks.assignee_not_allowed", "A Project Manager can assign only Team Members or themselves.");
}
