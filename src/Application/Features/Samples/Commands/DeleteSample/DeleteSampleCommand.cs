// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Application.Abstractions.Messaging;

namespace Contoso.TemplateApp.Application.Features.Samples.Commands.DeleteSample;

/// <summary>Soft-deletes a Sample.</summary>
public sealed record DeleteSampleCommand(Guid Id) : ICommand;
