namespace Datavanced.SmartTaskManagement.Application.Features.Users;

/// <summary>A user that can be picked as an assignee: id, name and email only (no role).</summary>
public sealed record AssignableUserDto(Guid Id, string FullName, string Email);
