namespace Datavanced.SmartTaskManagement.Application.Features.Dashboard;

/// <summary>Dashboard numbers over the tasks and projects the caller may see.</summary>
/// <param name="TotalProjects">Visible projects.</param>
/// <param name="TotalTasks">Visible tasks, all statuses.</param>
/// <param name="TasksByStatus">All four statuses, zero when none.</param>
/// <param name="TasksByPriority">All four priorities, zero when none.</param>
/// <param name="CompletedTasks">Tasks with status Completed.</param>
/// <param name="PendingTasks">ToDo plus InProgress; Cancelled is in neither group.</param>
/// <param name="OverdueTasks">Pending tasks with a due date before today.</param>
public sealed record DashboardSummaryDto(
    int TotalProjects,
    int TotalTasks,
    TaskStatusCountsDto TasksByStatus,
    TaskPriorityCountsDto TasksByPriority,
    int CompletedTasks,
    int PendingTasks,
    int OverdueTasks);

public sealed record TaskStatusCountsDto(int ToDo, int InProgress, int Completed, int Cancelled);

public sealed record TaskPriorityCountsDto(int Low, int Medium, int High, int Critical);
