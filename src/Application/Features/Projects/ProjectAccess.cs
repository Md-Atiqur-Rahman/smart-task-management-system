using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects;

/// <summary>Who may manage a project: an Admin, or the Project Manager who owns it (design section 3).</summary>
internal static class ProjectAccess
{
    public static bool CanManage(Project project, Guid callerId, Role callerRole) =>
        callerRole == Role.Admin || (callerRole == Role.ProjectManager && project.OwnerId == callerId);
}
