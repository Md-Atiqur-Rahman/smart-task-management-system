using Datavanced.SmartTaskManagement.Domain.Results;

namespace Datavanced.SmartTaskManagement.Application.Abstractions.Ai;

/// <summary>Expected failures of the AI text service. Codes are part of the API contract.</summary>
public static class AiErrors
{
    public static readonly Error NotConfigured =
        Error.Unavailable("ai.not_configured", "The AI service is not configured.");

    public static readonly Error Unavailable =
        Error.Unavailable("ai.unavailable", "The AI service is unavailable. Try again later.");

    public static readonly Error Rejected =
        Error.Validation("ai.rejected", "The AI service declined this text. Rewrite it and try again.");
}
