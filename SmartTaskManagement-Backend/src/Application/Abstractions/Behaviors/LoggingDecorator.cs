using Datavanced.SmartTaskManagement.Application.Abstractions.Messaging;
using Datavanced.SmartTaskManagement.Domain.Results;

using Microsoft.Extensions.Logging;

namespace Datavanced.SmartTaskManagement.Application.Abstractions.Behaviors;

/// <summary>
/// Pipeline step (outermost): logs the request name and the outcome.
/// Never logs request contents. Exceptions are not caught here; the Api's
/// global exception handler logs them.
/// </summary>
internal static partial class LoggingDecorator
{
    internal sealed class CommandHandler<TCommand, TResponse>(
        ICommandHandler<TCommand, TResponse> inner,
        ILogger<CommandHandler<TCommand, TResponse>> logger)
        : ICommandHandler<TCommand, TResponse>
        where TCommand : ICommand<TResponse>
    {
        public async Task<Result<TResponse>> Handle(
            TCommand command, CancellationToken cancellationToken)
        {
            var name = typeof(TCommand).Name;
            LogHandling(logger, name);
            var result = await inner.Handle(command, cancellationToken);
            LogOutcome(logger, name, result);
            return result;
        }
    }

    internal sealed class CommandBaseHandler<TCommand>(
        ICommandHandler<TCommand> inner,
        ILogger<CommandBaseHandler<TCommand>> logger)
        : ICommandHandler<TCommand>
        where TCommand : ICommand
    {
        public async Task<Result> Handle(TCommand command, CancellationToken cancellationToken)
        {
            var name = typeof(TCommand).Name;
            LogHandling(logger, name);
            var result = await inner.Handle(command, cancellationToken);
            LogOutcome(logger, name, result);
            return result;
        }
    }

    internal sealed class QueryHandler<TQuery, TResponse>(
        IQueryHandler<TQuery, TResponse> inner,
        ILogger<QueryHandler<TQuery, TResponse>> logger)
        : IQueryHandler<TQuery, TResponse>
        where TQuery : IQuery<TResponse>
    {
        public async Task<Result<TResponse>> Handle(
            TQuery query, CancellationToken cancellationToken)
        {
            var name = typeof(TQuery).Name;
            LogHandling(logger, name);
            var result = await inner.Handle(query, cancellationToken);
            LogOutcome(logger, name, result);
            return result;
        }
    }

    private static void LogOutcome(ILogger logger, string requestName, Result result)
    {
        if (result.IsSuccess)
        {
            LogHandled(logger, requestName);
        }
        else
        {
            LogFailed(logger, requestName, result.Error.Code);
        }
    }

    [LoggerMessage(Level = LogLevel.Information, Message = "Handling {RequestName}")]
    private static partial void LogHandling(ILogger logger, string requestName);

    [LoggerMessage(Level = LogLevel.Information, Message = "Handled {RequestName}")]
    private static partial void LogHandled(ILogger logger, string requestName);

    [LoggerMessage(
        Level = LogLevel.Warning,
        Message = "Handled {RequestName} with expected failure {ErrorCode}")]
    private static partial void LogFailed(ILogger logger, string requestName, string errorCode);
}
