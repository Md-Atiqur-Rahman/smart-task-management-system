using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Users;

/// <summary>A user as listed for an Admin. Never carries credentials.</summary>
public sealed record UserDto(Guid Id, string FullName, string Email, Role Role, DateTimeOffset CreatedAt);
