using Datavanced.SmartTaskManagement.Application.Abstractions.Ai;
using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Infrastructure.Ai;

/// <summary>Placeholder until the Gemini implementation (Task 4) replaces it: always reports the service as not configured.</summary>
public sealed class UnconfiguredAiTextImprover : IAiTextImprover
{
    public Task<Result<string>> ImproveDescriptionAsync(string? title, string description, CancellationToken cancellationToken) =>
        Task.FromResult(Result.Failure<string>(AiErrors.NotConfigured));
}
