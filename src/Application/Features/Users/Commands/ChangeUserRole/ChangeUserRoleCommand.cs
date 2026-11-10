using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Features.Users.Commands.ChangeUserRole;

/// <summary>Admin changes a user's role; the user's existing sessions end.</summary>
public sealed record ChangeUserRoleCommand(Guid UserId, Role Role) : ICommand;
