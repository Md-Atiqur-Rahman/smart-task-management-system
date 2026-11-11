using System.Collections.Concurrent;

using Datavanced.SmartTaskManagement.Application.Abstractions.Email;

namespace Datavanced.SmartTaskManagement.IntegrationTests.Api.TestSupport;

/// <summary>Records every message instead of sending it; optionally throws to simulate a failed send.</summary>
public sealed class FakeEmailSender : IEmailSender
{
    private readonly ConcurrentQueue<EmailMessage> _sent = new();

    private int _attempts;

    /// <summary>When set, called before recording; an exception it returns is thrown instead of sending.</summary>
    public Func<EmailMessage, Exception?>? Failure { get; set; }

    /// <summary>Messages that were "sent" successfully.</summary>
    public IReadOnlyList<EmailMessage> Sent => _sent.ToArray();

    /// <summary>Number of SendAsync calls, failed ones included.</summary>
    public int Attempts => Volatile.Read(ref _attempts);

    public Task SendAsync(EmailMessage message, CancellationToken ct)
    {
        Interlocked.Increment(ref _attempts);
        var failure = Failure?.Invoke(message);
        if (failure is not null)
        {
            throw failure;
        }

        _sent.Enqueue(message);
        return Task.CompletedTask;
    }
}
