using Datavanced.SmartTaskManagement.Domain.Projects;
using Datavanced.SmartTaskManagement.Domain.Tasks;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.ReadRepositories;

/// <summary>
/// The one definition of what a caller may see, shared by every read repository.
/// Tasks: Admin: every task; Project Manager: every task of an own project plus own assignments;
/// any other role: own assignments; always only non-deleted tasks of non-deleted projects.
/// Projects: Admin: all; Project Manager: own projects plus projects with a task assigned to them;
/// any other role: projects with a task assigned to them; only non-deleted tasks count.
/// The soft-delete checks are explicit on purpose: a query that uses IgnoreQueryFilters() (for
/// example the assignee or owner lookup on Users) switches off the global query filters for the
/// WHOLE query, the Tasks and Projects filters included.
/// </summary>
internal static class Visibility
{
    public static IQueryable<TaskItem> Tasks(AppDbContext context) =>
        context.Set<TaskItem>().Where(t => !t.IsDeleted);

    public static IQueryable<Project> Projects(AppDbContext context) =>
        context.Set<Project>().Where(p => !p.IsDeleted);

    public static IQueryable<TaskItem> VisibleTasks(AppDbContext context, Guid callerId, Role callerRole)
    {
        var tasks = Tasks(context);
        var projects = Projects(context);
        return callerRole switch
        {
            Role.Admin => tasks.Where(t => projects.Any(p => p.Id == t.ProjectId)),
            Role.ProjectManager => tasks.Where(t =>
                projects.Any(p => p.Id == t.ProjectId && (p.OwnerId == callerId || t.AssigneeId == callerId))),
            _ => tasks.Where(t => t.AssigneeId == callerId && projects.Any(p => p.Id == t.ProjectId)),
        };
    }

    public static IQueryable<Project> VisibleProjects(AppDbContext context, Guid callerId, Role callerRole)
    {
        var projects = Projects(context);
        var tasks = Tasks(context);
        return callerRole switch
        {
            Role.Admin => projects,
            Role.ProjectManager => projects.Where(p =>
                p.OwnerId == callerId || tasks.Any(t => t.ProjectId == p.Id && t.AssigneeId == callerId)),
            _ => projects.Where(p => tasks.Any(t => t.ProjectId == p.Id && t.AssigneeId == callerId)),
        };
    }

    // The tasks a caller's project counts include: Admin: every task; Project Manager: every task
    // of an own project plus own assignments; any other role: own assignments. Non-deleted only.
    public static IQueryable<TaskItem> CountedTasks(AppDbContext context, Guid callerId, Role callerRole)
    {
        var tasks = Tasks(context);
        var projects = Projects(context);
        return callerRole switch
        {
            Role.Admin => tasks,
            Role.ProjectManager => tasks.Where(t =>
                t.AssigneeId == callerId || projects.Any(p => p.Id == t.ProjectId && p.OwnerId == callerId)),
            _ => tasks.Where(t => t.AssigneeId == callerId),
        };
    }
}
