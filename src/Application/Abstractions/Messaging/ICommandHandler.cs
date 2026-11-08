using Contoso.TemplateApp.Domain.Results;

namespace Contoso.TemplateApp.Application.Abstractions.Messaging;

/// <summary>Handles a command without a return value. Writes go through a repository and IUnitOfWork.</summary>
public interface ICommandHandler<in TCommand>
    where TCommand : ICommand
{
    Task<Result> Handle(TCommand command, CancellationToken cancellationToken);
}

/// <summary>Handles a command that returns a value.</summary>
public interface ICommandHandler<in TCommand, TResponse>
    where TCommand : ICommand<TResponse>
{
    Task<Result<TResponse>> Handle(TCommand command, CancellationToken cancellationToken);
}
