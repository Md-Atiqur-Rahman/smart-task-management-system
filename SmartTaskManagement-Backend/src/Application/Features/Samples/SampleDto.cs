// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Domain.Samples;

namespace Datavanced.SmartTaskManagement.Application.Features.Samples;

/// <summary>Read model returned by the API. Never the entity itself.</summary>
public sealed record SampleDto(
    Guid Id,
    string Name,
    string? Description,
    SampleStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
