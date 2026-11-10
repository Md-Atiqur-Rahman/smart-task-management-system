namespace Datavanced.SmartTaskManagement.Api.Controllers.Auth;

/// <summary>HTTP body for POST /api/auth/register. Nullable so a missing value reaches FluentValidation.</summary>
public sealed record RegisterRequest(string? Email, string? FullName, string? Password);
