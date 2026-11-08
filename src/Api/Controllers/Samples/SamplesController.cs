// SAMPLE: copy-source, delete or rename
using Contoso.TemplateApp.Api.Errors;
using Contoso.TemplateApp.Application.Abstractions.Messaging;
using Contoso.TemplateApp.Application.Abstractions.Paging;
using Contoso.TemplateApp.Application.Features.Samples;
using Contoso.TemplateApp.Application.Features.Samples.Commands.CompleteSample;
using Contoso.TemplateApp.Application.Features.Samples.Commands.CreateSample;
using Contoso.TemplateApp.Application.Features.Samples.Commands.DeleteSample;
using Contoso.TemplateApp.Application.Features.Samples.Queries.GetSampleById;
using Contoso.TemplateApp.Application.Features.Samples.Queries.ListSamples;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Contoso.TemplateApp.Api.Controllers.Samples;

/// <summary>
/// Thin controller: binds the request, calls a handler, maps the Result.
/// No repository, DbContext or business logic here.
/// </summary>
[ApiController]
[Route(Route)]
public sealed class SamplesController : ControllerBase
{
    public const string Route = "api/samples";

    // SAMPLE ONLY: replace with [Authorize] in Phase 2
    [AllowAnonymous]
    [HttpGet]
    [ProducesResponseType<PagedResult<SampleDto>>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    public async Task<IActionResult> List(
        [FromQuery] ListSamplesRequest request,
        [FromServices] IQueryHandler<ListSamplesQuery, PagedResult<SampleDto>> handler,
        [FromServices] IOptions<PagingOptions> paging,
        CancellationToken cancellationToken)
    {
        var query = new ListSamplesQuery(
            request.Search,
            request.SortBy,
            request.SortDirection,
            request.Page,
            request.PageSize ?? paging.Value.DefaultPageSize);

        var result = await handler.Handle(query, cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    // SAMPLE ONLY: replace with [Authorize] in Phase 2
    [AllowAnonymous]
    [HttpGet("{id:guid}")]
    [ProducesResponseType<SampleDto>(StatusCodes.Status200OK)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] IQueryHandler<GetSampleByIdQuery, SampleDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new GetSampleByIdQuery(id), cancellationToken);
        return result.IsSuccess ? Ok(result.Value) : this.ToProblem(result.Error);
    }

    // SAMPLE ONLY: replace with [Authorize] in Phase 2
    [AllowAnonymous]
    [HttpPost]
    [ProducesResponseType<CreatedResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest, ErrorResults.ProblemJson)]
    public async Task<IActionResult> Create(
        [FromBody] CreateSampleRequest request,
        [FromServices] ICommandHandler<CreateSampleCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(
            new CreateSampleCommand(request.Name ?? string.Empty, request.Description),
            cancellationToken);

        return result.IsSuccess
            ? Created(new Uri($"/{Route}/{result.Value}", UriKind.Relative), new CreatedResponse(result.Value))
            : this.ToProblem(result.Error);
    }

    // SAMPLE ONLY: replace with [Authorize] in Phase 2
    [AllowAnonymous]
    [HttpPost("{id:guid}/complete")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ErrorResults.ProblemJson)]
    public async Task<IActionResult> Complete(
        Guid id,
        [FromServices] ICommandHandler<CompleteSampleCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new CompleteSampleCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }

    // SAMPLE ONLY: replace with [Authorize] in Phase 2
    [AllowAnonymous]
    [HttpDelete("{id:guid}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound, ErrorResults.ProblemJson)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict, ErrorResults.ProblemJson)]
    public async Task<IActionResult> Delete(
        Guid id,
        [FromServices] ICommandHandler<DeleteSampleCommand> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.Handle(new DeleteSampleCommand(id), cancellationToken);
        return result.IsSuccess ? NoContent() : this.ToProblem(result.Error);
    }
}
