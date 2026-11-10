// SAMPLE: copy-source, delete or rename
using Datavanced.SmartTaskManagement.Api.Errors;
using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Application.Abstractions.Paging;
using Datavanced.SmartTaskManagement.Application.Features.Samples;
using Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.CompleteSample;
using Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.CreateSample;
using Datavanced.SmartTaskManagement.Application.Features.Samples.Commands.DeleteSample;
using Datavanced.SmartTaskManagement.Application.Features.Samples.Queries.GetSampleById;
using Datavanced.SmartTaskManagement.Application.Features.Samples.Queries.ListSamples;

using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Datavanced.SmartTaskManagement.Api.Controllers.Samples;

/// <summary>
/// Thin controller: binds the request, calls a handler, maps the Result.
/// No repository, DbContext or business logic here.
/// </summary>
[ApiController]
[Route(Route)]
public sealed class SamplesController : ControllerBase
{
    public const string Route = "api/samples";

    [Authorize]
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

    [Authorize]
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

    [Authorize]
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

    [Authorize]
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

    [Authorize]
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
