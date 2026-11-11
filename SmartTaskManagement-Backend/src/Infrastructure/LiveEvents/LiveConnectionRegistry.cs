using System.Collections.Concurrent;
using System.Threading.Channels;

using Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;
using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Infrastructure.LiveEvents;

/// <summary>
/// In-memory registry of the live streams open on this instance (one per browser tab). Each connection
/// has its own bounded channel that drops the oldest event when full, so a slow or closed connection
/// never blocks delivery to the others. Events are hints; the client re-fetches (ADR 0018).
/// </summary>
public sealed class LiveConnectionRegistry : ILiveConnections
{
    /// <summary>Events a connection may hold before the oldest is dropped.</summary>
    public const int ConnectionCapacity = 64;

    private readonly ConcurrentDictionary<Guid, Connection> _connections = new();

    public ILiveConnection Open(Guid userId, Role role)
    {
        var connection = new Connection(this, userId, role);
        _connections[connection.Id] = connection;
        return connection;
    }

    /// <summary>Writes the event to every matching local connection (the user's tabs, and Admins when requested).</summary>
    public void Deliver(LiveEvent liveEvent)
    {
        foreach (var connection in _connections.Values)
        {
            var matches = (liveEvent.IncludeAdmins && connection.Role == Role.Admin)
                || liveEvent.UserIds.Contains(connection.UserId);
            if (matches)
            {
                // DropOldest: TryWrite fails only when the connection is already completed.
                connection.Writer.TryWrite(liveEvent);
            }
        }
    }

    private sealed class Connection : ILiveConnection
    {
        private readonly LiveConnectionRegistry _owner;
        private readonly Channel<LiveEvent> _channel = Channel.CreateBounded<LiveEvent>(
            new BoundedChannelOptions(ConnectionCapacity)
            {
                FullMode = BoundedChannelFullMode.DropOldest,
                SingleReader = true,
            });

        public Connection(LiveConnectionRegistry owner, Guid userId, Role role)
        {
            _owner = owner;
            UserId = userId;
            Role = role;
        }

        public Guid Id { get; } = Guid.NewGuid();

        public Guid UserId { get; }

        public Role Role { get; }

        public ChannelWriter<LiveEvent> Writer => _channel.Writer;

        public ChannelReader<LiveEvent> Events => _channel.Reader;

        public void Dispose()
        {
            _owner._connections.TryRemove(Id, out _);
            _channel.Writer.TryComplete();
        }
    }
}
