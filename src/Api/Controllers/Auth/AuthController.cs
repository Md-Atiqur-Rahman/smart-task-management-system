using Datavanced.SmartTaskManagement.Api.Auth;
using Datavanced.SmartTaskManagement.Api.Errors;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Features.Auth;
using Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.Login;
using Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.Logout;
using Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.RefreshSession;
using Datavanced.SmartTaskManagement.Application.Features.Auth.Commands.RegisterUser;
using Datavanced.SmartTaskManagement.Application.Features.Auth.Queries.GetCurrentUser;
using Datavanced.SmartTaskManagement.Domain.Auth;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Datavanced.SmartTaskManagement.Api.Controllers.Auth;

/// <summary>
/// Thin controller: binds the request, calls a handler, maps the Result. The refresh token
/// travels only in the HttpOnly cookie. Register, Login, Refresh and Logout are the only
/// anonymous actions in the Api (ADR 0014).
/// </summary>
[ApiController]
[Route(Route)]
public sealed class AuthController : ControllerBase
{
    public const string Route = "api/auth";

    [AllowAnonymous]
    [HttpPost("register")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ErrorResults.ProblemJson)]
    public async Task<IActionResult> Register(
        [FromBody] RegisterRequest request,
        [FromServices] ICommandHandler<RegisterUserCommand, AuthTokens> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new RegisterUserCommand(
                request.Email ?? string.Empty, request.FullName ?? string.Empty, request.Password ?? string.Empty),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToProblem(result.Error);
        }

        RefreshTokenCookie.Append(Response, result.Value.RefreshToken, result.Value.RefreshTokenExpiresAt);
        return Created(new Uri("/api/auth/me", UriKind.Relative), ToResponse(result.Value));
    }

    [AllowAnonymous]
    [HttpPost("login")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    public async Task<IActionResult> Login(
        [FromBody] LoginRequest request,
        [FromServices] ICommandHandler<LoginCommand, AuthTokens> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new LoginCommand(request.Email ?? string.Empty, request.Password ?? string.Empty),
            cancellationToken);

        if (result.IsFailure)
        {
            return this.ToProblem(result.Error);
        }

        RefreshTokenCookie.Append(Response, result.Value.RefreshToken, result.Value.RefreshTokenExpiresAt);
        return Ok(ToResponse(result.Value));
    }

    [AllowAnonymous]
    [RequireAllowedOrigin]
    [HttpPost("refresh")]
    [ProducesResponseType<AuthResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ErrorResults.ProblemJson)]
    public async Task<IActionResult> Refresh(
        [FromServices] ICommandHandler<RefreshSessionCommand, AuthTokens> handler,
        CancellationToken cancellationToken)
    {
        var cookie = RefreshTokenCookie.Read(Request);
        if (cookie is null)
        {
            return this.ToProblem(AuthErrors.InvalidRefreshToken);
        }

        var result = await handler.Handle(new RefreshSessionCommand(cookie), cancellationToken);
        if (result.IsFailure)
        {
            // A superseded token lost a benign race: the browser already holds the new cookie, keep it.
            if (string.Equals(result.Error.Code, AuthErrors.InvalidRefreshToken.Code, StringComparison.Ordinal))
            {
                RefreshTokenCookie.Delete(Response);
            }

            return this.ToProblem(result.Error);
        }

        RefreshTokenCookie.Append(Response, result.Value.RefreshToken, result.Value.RefreshTokenExpiresAt);
        return Ok(ToResponse(result.Value));
    }

    [AllowAnonymous]
    [RequireAllowedOrigin]
    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status403Forbidden, ErrorResults.ProblemJson)]
    public async Task<IActionResult> Logout(
        [FromServices] ICommandHandler<LogoutCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new LogoutCommand(RefreshTokenCookie.Read(Request)), cancellationToken);
        if (result.IsFailure)
        {
            return this.ToProblem(result.Error);
        }

        RefreshTokenCookie.Delete(Response);
        return NoContent();
    }

    [Authorize]
    [HttpGet("me")]
    [ProducesResponseType<CurrentUserDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable, ErrorResults.ProblemJson)]
    public async Task<IActionResult> Me(
        [FromServices] IQueryHandler<GetCurrentUserQuery, CurrentUserDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetCurrentUserQuery(), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    private static AuthResponse ToResponse(AuthTokens tokens) =>
        new(tokens.AccessToken.Value, tokens.AccessToken.ExpiresAt, tokens.User);
}
