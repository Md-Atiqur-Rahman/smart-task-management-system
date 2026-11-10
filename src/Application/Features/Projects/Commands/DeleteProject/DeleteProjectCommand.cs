using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.DeleteProject;

/// <summary>Soft-deletes a project.</summary>
public sealed record DeleteProjectCommand(Guid Id) : ICommand;
