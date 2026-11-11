using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Application.Abstractions.Ai;

/// <summary>Rewrites a task description into a clearer, structured one. Failures come back as <see cref="AiErrors"/>.</summary>
public interface IAiTextImprover
{
    Task<Result<string>> ImproveDescriptionAsync(string? title, string description, CancellationToken cancellationToken);
}
