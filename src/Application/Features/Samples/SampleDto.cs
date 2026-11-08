// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Domain.Samples;

namespace Contoso.TemplateApp.Application.Features.Samples;

/// <summary>Read model returned by the API. Never the entity itself.</summary>
public sealed record SampleDto(
    Guid Id,
    string Name,
    string? Description,
    SampleStatus Status,
    DateTimeOffset CreatedAt,
    DateTimeOffset? UpdatedAt);
