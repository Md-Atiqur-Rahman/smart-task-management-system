using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.Login;

/// <summary>Signs a user in with email and password.</summary>
public sealed record LoginCommand(string Email, string Password) : ICommand<AuthTokens>;
