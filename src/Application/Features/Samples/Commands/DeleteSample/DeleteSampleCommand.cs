// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.DeleteSample;

/// <summary>Soft-deletes a Sample.</summary>
public sealed record DeleteSampleCommand(Guid Id) : ICommand;
