using System.Collections.Concurrent;

using Microsoft.Extensions.Logging;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

/// <summary>ILogger that keeps every entry (level, rendered message, exception text) so tests can assert on logs.</summary>
public sealed class CapturingLogger<T> : ILogger<T>
{
    private readonly ConcurrentQueue<CapturedLog> _entries = new();

    public IReadOnlyCollection<CapturedLog> Entries => _entries.ToArray();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        _entries.Enqueue(new CapturedLog(logLevel, formatter(state, exception), exception?.ToString()));
    }
}

public sealed record CapturedLog(LogLevel Level, string Message, string? Exception)
{
    /// <summary>Everything a log sink could write for this entry.</summary>
    public string AllText => Message + " " + Exception;
}
