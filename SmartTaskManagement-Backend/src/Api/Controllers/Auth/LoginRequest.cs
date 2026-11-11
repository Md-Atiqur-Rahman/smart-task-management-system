namespace Datavanced.SmartTaskManagement.Api.Controllers.Auth;

/// <summary>HTTP body for POST /api/auth/login. Nullable so a missing value reaches FluentValidation.</summary>
public sealed record LoginRequest(string? Email, string? Password);
