using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth.Queries.GetCurrentUser;

/// <summary>The signed-in user, taken from the security context.</summary>
public sealed record GetCurrentUserQuery : IQuery<CurrentUserDto>;
