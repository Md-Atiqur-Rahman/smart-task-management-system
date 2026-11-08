// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.CompleteSample;

/// <summary>Moves a Sample from Open to Completed.</summary>
public sealed record CompleteSampleCommand(Guid Id) : ICommand;
