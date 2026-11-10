using Datavanced.SmartTaskManagement.Application.Features.Auth;

namespace Datavanced.SmartTaskManagement.Api.Controllers.Auth;

/// <summary>Response of register, login and refresh. The refresh token is only in the cookie, never here.</summary>
public sealed record AuthResponse(string AccessToken, DateTimeOffset ExpiresAt, CurrentUserDto User);
