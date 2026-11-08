using System.Collections.Concurrent;

using Serilog.Core;
using Serilog.Events;

namespace Contoso.TemplateApp.IntegrationTests.Api.TestSupport;

/// <summary>Serilog sink that keeps log events in memory so tests can assert on them.</summary>
public sealed class CapturingSink : ILogEventSink
{
    private readonly ConcurrentQueue<LogEvent> _events = new();

    public IReadOnlyCollection<LogEvent> Events => _events.ToArray();

    public void Emit(LogEvent logEvent) => _events.Enqueue(logEvent);
}
