using System.Linq.Expressions;

using Datavanced.SmartTaskManagement.Domain.Tasks;

namespace Datavanced.SmartTaskManagement.Infrastructure.Persistence.ReadRepositories;

/// <summary>
/// Enums are stored as strings, so priority and status order by an explicit rank (SQL CASE), never
/// alphabetically. Every enum value has its own rank; an unknown stored value falls back to
/// UnknownRank (after every known value). Shared by the task list sort and the dashboard.
/// </summary>
internal static class TaskRanks
{
    private const int UnknownRank = 4;

    public static readonly Expression<Func<TaskItem, int>> Priority = t =>
        t.Priority == TaskPriority.Low ? 0
        : t.Priority == TaskPriority.Medium ? 1
        : t.Priority == TaskPriority.High ? 2
        : t.Priority == TaskPriority.Critical ? 3
        : UnknownRank;

    public static readonly Expression<Func<TaskItem, int>> Status = t =>
        t.Status == TaskItemStatus.ToDo ? 0
        : t.Status == TaskItemStatus.InProgress ? 1
        : t.Status == TaskItemStatus.Completed ? 2
        : t.Status == TaskItemStatus.Cancelled ? 3
        : UnknownRank;

    /// <summary>Boxes a rank expression for the object-typed sort whitelist (same SQL as the unboxed one).</summary>
    public static Expression<Func<TaskItem, object?>> AsSortKey(Expression<Func<TaskItem, int>> rank) =>
        Expression.Lambda<Func<TaskItem, object?>>(Expression.Convert(rank.Body, typeof(object)), rank.Parameters);
}
