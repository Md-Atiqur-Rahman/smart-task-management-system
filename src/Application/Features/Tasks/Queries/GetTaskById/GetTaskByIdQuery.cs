using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Queries.GetTaskById;

public sealed record GetTaskByIdQuery(Guid Id) : IQuery<TaskDetailsDto>;
