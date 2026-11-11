using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Dashboard.Queries.GetUpcomingTasks;

/// <summary>The caller's pending tasks due within the configured window.</summary>
public sealed record GetUpcomingTasksQuery : IQuery<IReadOnlyList<UpcomingTaskDto>>;
