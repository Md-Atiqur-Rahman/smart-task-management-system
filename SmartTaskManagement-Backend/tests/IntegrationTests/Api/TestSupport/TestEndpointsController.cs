using Datavanced.SmartTaskManagement.Api.Errors;
using Datavanced.SmartTaskManagement.Application.Abstractions.Security;
using Datavanced.SmartTaskManagement.Domain.Results;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

/// <summary>Test-only endpoints, added to the test host only (ApiFactory).</summary>
[ApiController]
[Route("test")]
public sealed class TestEndpointsController : ControllerBase
{
    public const string SecretDetail = "secret-detail-that-must-only-be-logged";

    /// <summary>Requires a valid token; returns what ISecurityContext saw.</summary>
    [HttpGet("whoami")]
    [Authorize]
    public IActionResult WhoAmI([FromServices] ISecurityContext securityContext) =>
        Ok(new WhoAmIResponse(
            securityContext.IsAuthenticated, securityContext.UserId, securityContext.Roles));

    [HttpGet("anonymous")]
    [AllowAnonymous]
    public IActionResult Anonymous([FromServices] ISecurityContext securityContext) =>
        Ok(new WhoAmIResponse(
            securityContext.IsAuthenticated, securityContext.UserId, securityContext.Roles));

    [AllowAnonymous]
    [HttpGet("errors/{type}")]
    public IActionResult Error(ErrorType type) =>
        type == ErrorType.Validation
            ? this.ToProblem(new ValidationError(
                new Dictionary<string, string[]> { ["Name"] = ["Name is required."] }))
            : this.ToProblem(new Error("test." + type.ToString().ToUpperInvariant(), "Test error.", type));

    /// <summary>The client goes away mid-request: the request is aborted and the work is cancelled.</summary>
    [AllowAnonymous]
    [HttpGet("abort")]
    public IActionResult Abort()
    {
        HttpContext.Abort();
        HttpContext.RequestAborted.ThrowIfCancellationRequested();
        return Ok();
    }

    /// <summary>A cancellation that is NOT a client abort (for example an internal timeout).</summary>
    [AllowAnonymous]
    [HttpGet("cancel-without-abort")]
    public IActionResult CancelWithoutAbort() =>
        throw new OperationCanceledException(SecretDetail, new CancellationToken(canceled: true))
        {
            Source = ControllerContext.ActionDescriptor.DisplayName,
        };

    [AllowAnonymous]
    [HttpGet("throw")]
    public IActionResult Throw() =>
        throw new InvalidOperationException(SecretDetail)
        {
            Source = ControllerContext.ActionDescriptor.DisplayName,
        };
}
