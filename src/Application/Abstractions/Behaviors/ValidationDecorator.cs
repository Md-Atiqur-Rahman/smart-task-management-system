using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Domain.Results;

using FluentValidation;

namespace Datavanced.SmartTaskManagement.Application.Abstractions.Behaviors;

/// <summary>
/// Pipeline step: validates the request and returns a ValidationError
/// WITHOUT calling the handler when validation fails.
/// </summary>
internal static class ValidationDecorator
{
    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> inner,
        IEnumerable<IValidator<TCommand>> validators)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(
            TCommand command, CancellationToken cancellationToken)
        {
            var error = await RequestValidation.ValidateAsync(command, validators, cancellationToken);
            return error is null
                ? await inner.Handle(command, cancellationToken)
                : Result.Failure<TResponse>(error);
        }
    }

    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> inner,
        IEnumerable<IValidator<TCommand>> validators)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var error = await RequestValidation.ValidateAsync(command, validators, cancellationToken);
            return error is null
                ? await inner.Handle(command, cancellationToken)
                : Result.Failure(error);
        }
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> inner,
        IEnumerable<IValidator<TQuery>> validators)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(
            TQuery query, CancellationToken cancellationToken)
        {
            var error = await RequestValidation.ValidateAsync(query, validators, cancellationToken);
            return error is null
                ? await inner.Handle(query, cancellationToken)
                : Result.Failure<TResponse>(error);
        }
    }
}
