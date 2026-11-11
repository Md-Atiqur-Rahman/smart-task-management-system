using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.UpdateProject;

/// <summary>Updates a project's name and description.</summary>
public sealed record UpdateProjectCommand(Guid Id, string Name, string? Description) : ICommand;
