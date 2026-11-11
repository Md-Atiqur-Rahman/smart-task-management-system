using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Tasks.Commands.ImproveTaskDescription;

/// <summary>Asks the AI service to rewrite a draft task description; nothing is stored.</summary>
public sealed record ImproveTaskDescriptionCommand(string? Title, string Description) : ICommand<ImprovedDescriptionDto>;
