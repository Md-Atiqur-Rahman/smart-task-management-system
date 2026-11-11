using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.CreateProject;

/// <summary>Creates a project. Returns the new Id. OwnerId is required for an Admin, optional for a Project Manager.</summary>
public sealed record CreateProjectCommand(string Name, string? Description, Guid? OwnerId) : ICommand<Guid>;
