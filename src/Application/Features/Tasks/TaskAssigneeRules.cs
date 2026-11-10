using Datavanced.SmartTaskManagement.Domain.Results;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks;

/// <summary>
/// Who may be assigned (design section 3): an Admin may assign any existing user; a Project Manager
/// only a Team Member or themselves. Unassigning (null) is not checked here; a manager may always do it.
/// </summary>
internal static class TaskAssigneeRules
{
    /// <summary>Returns the failure, or null when the assignment is allowed.</summary>
    public static async Task<Error?> CheckAsync(
        Guid assigneeId,
        Guid callerId,
        Role callerRole,
        IUserRepository users,
        CancellationToken ct)
    {
        var assignee = await users.GetByIdAsync(assigneeId, ct);
        if (assignee is null)
        {
            return TaskErrors.InvalidAssignee;
        }

        if (callerRole != Role.Admin && assignee.Role != Role.TeamMember && assignee.Id != callerId)
        {
            return TaskErrors.AssigneeNotAllowed;
        }

        return null;
    }
}
