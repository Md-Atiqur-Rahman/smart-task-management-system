using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Projects.Commands.ChangeProjectOwner;

/// <summary>Admin hands a project to another Project Manager.</summary>
public sealed record ChangeProjectOwnerCommand(Guid Id, Guid OwnerId) : ICommand;
