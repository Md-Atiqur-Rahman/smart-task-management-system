using System.Threading.Channels;

using Datavanced.SmartTaskManagement.Domain.Users;

namespace Datavanced.SmartTaskManagement.Application.Abstractions.LiveEvents;

/// <summary>The open live streams of this instance.</summary>
public interface ILiveConnections
{
    /// <summary>Registers a stream for a user (one per browser tab). Dispose it when the stream ends.</summary>
    ILiveConnection Open(Guid userId, Role role);
}

/// <summary>One open live stream. Disposing it removes it from the registry and completes <see cref="Events"/>.</summary>
public interface ILiveConnection : IDisposable
{
    ChannelReader<LiveEvent> Events { get; }
}
