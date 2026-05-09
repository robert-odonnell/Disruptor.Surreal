using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Connection;

/// <summary>
/// Abstraction over a transport-layer SurrealDB connection. v1 has a single
/// implementation (<c>WebSocketConnection</c>); HTTP will land later.
/// </summary>
internal interface IConnection : IAsyncDisposable
{
    /// <summary>
    /// Sends <paramref name="command"/> over the wire and awaits the matching reply.
    /// Throws <see cref="SurrealRpcException"/> on a server error.
    /// </summary>
    Task<Value> SendAsync(Command command, CancellationToken ct = default);

    /// <summary>True while the connection is alive and sending.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Optional re-auth callback. Invoked when an in-flight RPC fails with a token-expired
    /// auth error; the handler should restore authentication, after which the original
    /// request is retried once. Set by the client after a successful signin.
    /// </summary>
    Func<CancellationToken, Task>? ReauthHandler { get; set; }

    /// <summary>
    /// Register a channel writer to receive notifications for a particular live query id.
    /// The receive loop dispatches incoming live frames to the matching writer.
    /// </summary>
    void RegisterLiveSubscription(Guid liveQueryId, System.Threading.Channels.ChannelWriter<Notification> writer);

    /// <summary>
    /// Remove a live subscription. Called by <see cref="LiveQueryHandle"/> on disposal
    /// after a best-effort kill RPC, or on connection drop to drain the registry.
    /// </summary>
    void UnregisterLiveSubscription(Guid liveQueryId);
}
