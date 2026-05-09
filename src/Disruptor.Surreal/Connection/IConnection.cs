using System.Threading.Channels;
using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Connection;

/// <summary>
/// Wire-level abstraction over a SurrealDB connection. Implement this to fake the
/// transport layer for unit-testing code that holds a <see cref="Surreal"/>; the
/// SDK provides one production implementation (WebSocket + CBOR).
/// </summary>
/// <remarks>
/// The <c>method</c> + <c>params</c> + <c>txnId</c> shape mirrors SurrealDB's RPC
/// envelope directly: methods are the protocol-level names (<c>"query"</c>,
/// <c>"signin"</c>, <c>"begin"</c>, …) and params is whatever <see cref="Value"/>
/// payload that method expects (typically an <see cref="ArrayValue"/>).
/// </remarks>
public interface IConnection : IAsyncDisposable
{
    /// <summary>
    /// Send an RPC and await the matching reply. <paramref name="method"/> is the
    /// SurrealDB RPC method name (<c>"query"</c>, <c>"signin"</c>, etc.);
    /// <paramref name="params"/> is the wire-level <see cref="Value"/> payload (usually
    /// an <see cref="ArrayValue"/>) or <c>null</c> when the method takes none;
    /// <paramref name="txnId"/> threads the request inside an open transaction.
    /// </summary>
    /// <exception cref="SurrealRpcException">Server-side failure (typed subclasses for auth / conflict / etc.).</exception>
    /// <exception cref="SurrealConnectionException">Connection-level failure (drop / handshake / etc.).</exception>
    Task<Value> SendAsync(string method, Value? @params, Guid? txnId, CancellationToken ct = default);

    /// <summary>True while the connection is alive and sending.</summary>
    bool IsConnected { get; }

    /// <summary>
    /// Optional re-auth callback. Invoked when an in-flight RPC fails with a token-expired
    /// auth error; the handler should restore authentication, after which the original
    /// request is retried once. Set by <see cref="Surreal"/> after a successful signin.
    /// </summary>
    Func<CancellationToken, Task>? ReauthHandler { get; set; }

    /// <summary>
    /// Register a channel writer to receive notifications for a particular live query id.
    /// The receive loop dispatches incoming live frames to the matching writer.
    /// </summary>
    void RegisterLiveSubscription(Guid liveQueryId, ChannelWriter<Notification> writer);

    /// <summary>
    /// Remove a live subscription. Called by <see cref="LiveQueryHandle"/> on disposal
    /// after a best-effort kill RPC, or on connection drop to drain the registry.
    /// </summary>
    void UnregisterLiveSubscription(Guid liveQueryId);
}
