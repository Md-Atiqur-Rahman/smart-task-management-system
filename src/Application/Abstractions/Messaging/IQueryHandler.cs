using Contoso.TemplateApp.Domain.Results;

namespace Contoso.TemplateApp.Application.Abstractions.Messaging;

/// <summary>
/// Handles a query. Injects only read repositories; never a write repository,
/// IUnitOfWork or DbContext, and never loads an aggregate.
/// </summary>
public interface IQueryHandler<in TQuery, TResponse>
    where TQuery : IQuery<TResponse>
{
    Task<Result<TResponse>> Handle(TQuery query, CancellationToken cancellationToken);
}
