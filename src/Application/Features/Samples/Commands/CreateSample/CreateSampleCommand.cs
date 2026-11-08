// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.CreateSample;

/// <summary>Creates a Sample. Returns the new Id.</summary>
public sealed record CreateSampleCommand(string Name, string? Description) : ICommand<Guid>;
