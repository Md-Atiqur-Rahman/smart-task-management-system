using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.RefreshSession;

/// <summary>Exchanges a refresh token for a new token pair (rotation).</summary>
public sealed record RefreshSessionCommand(string RefreshToken) : ICommand<AuthTokens>;
