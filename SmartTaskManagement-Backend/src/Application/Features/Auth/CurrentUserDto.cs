using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth;

/// <summary>The signed-in user as returned by the auth endpoints.</summary>
public sealed record CurrentUserDto(Guid Id, string FullName, string Email, Role Role);
