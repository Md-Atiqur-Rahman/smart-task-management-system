using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.Logout;

/// <summary>Ends the session of one device by revoking its refresh token.</summary>
public sealed record LogoutCommand(string? RefreshToken) : ICommand;
