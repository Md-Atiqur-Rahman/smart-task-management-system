// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Application.Abstractions.Messaging;

namespace Contoso.TemplateApp.Application.Features.Samples.Commands.CompleteSample;

/// <summary>Moves a Sample from Open to Completed.</summary>
public sealed record CompleteSampleCommand(Guid Id) : ICommand;
