// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Application.Abstractions.Messaging;

namespace Contoso.TemplateApp.Application.Features.Samples.Commands.CreateSample;

/// <summary>Creates a Sample. Returns the new Id.</summary>
public sealed record CreateSampleCommand(string Name, string? Description) : ICommand<Guid>;
