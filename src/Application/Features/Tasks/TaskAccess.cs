using Datavanced.SmartTaskManagement.Application.Features.Projects;
using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks;

/// <summary>Who may see a task: a manager of its project, or its assignee whatever the role (design section 3).</summary>
internal static class TaskAccess
{
    public static bool CanSee(TaskItem task, Project project, Guid callerId, Role callerRole) =>
        ProjectAccess.CanManage(project, callerId, callerRole) || task.AssigneeId == callerId;
}
