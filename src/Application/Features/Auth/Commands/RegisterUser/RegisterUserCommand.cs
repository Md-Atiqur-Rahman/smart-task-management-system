using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;

namespace Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.RegisterUser;

/// <summary>Registers a new TeamMember account and signs it in.</summary>
public sealed record RegisterUserCommand(string Email, string FullName, string Password) : ICommand<AuthTokens>;
