using System.Buffers;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Threading.Channels;
using Disruptor.Surreal.Values;

namespace Disruptor.Surreal.Connection;

/// <summary>
/// A WebSocket transport for the SurrealDB RPC protocol. CBOR-framed binary messages
/// negotiated via <c>Sec-WebSocket-Protocol: cbor</c>.
/// </summary>
internal sealed class WebSocketConnection : IConnection
{
    private const string CborSubprotocol = "cbor";

    private readonly ClientWebSocket _socket;
    private readonly Endpoint _endpoint;
    private readonly Channel<RpcRequest> _outbound;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<RpcResponse>> _pending = new();
    private readonly ConcurrentDictionary<Guid, ChannelWriter<Notification>> _liveSubscriptions = new();
    private readonly CancellationTokenSource _shutdown = new();
    private readonly SemaphoreSlim _sendLock = new(1, 1);

    private long _nextId;
    private Task? _sendLoop;
    private Task? _receiveLoop;
    private Task? _pingLoop;
    private int _connected;
    private readonly SemaphoreSlim _reauthGate = new(1, 1);
    private long _reauthEpoch;

    /// <summary>
    /// Caller-installed re-auth callback. When a request fails with a token-expired
    /// <see cref="SurrealAuthException"/>, the connection invokes this to refresh
    /// authentication then retries the original request once. Set by the client
    /// after a successful signin.
    /// </summary>
    public Func<CancellationToken, Task>? ReauthHandler { get; set; }

    public void RegisterLiveSubscription(Guid liveQueryId, ChannelWriter<Notification> writer)
    {
        if (!_liveSubscriptions.TryAdd(liveQueryId, writer))
            throw new InvalidOperationException(
                $"Live subscription for {liveQueryId} is already registered.");
    }

    public void UnregisterLiveSubscription(Guid liveQueryId)
    {
        if (_liveSubscriptions.TryRemove(liveQueryId, out var writer))
            writer.TryComplete();
    }

    private WebSocketConnection(ClientWebSocket socket, Endpoint endpoint)
    {
        _socket = socket;
        _endpoint = endpoint;
        _outbound = Channel.CreateBounded<RpcRequest>(
            new BoundedChannelOptions(capacity: 1024)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait,
            });
    }

    public bool IsConnected => Volatile.Read(ref _connected) == 1
        && _socket.State == WebSocketState.Open;

    public static async Task<WebSocketConnection> ConnectAsync(Endpoint endpoint, CancellationToken ct)
    {
        var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol(CborSubprotocol);
        socket.Options.KeepAliveInterval = TimeSpan.Zero; // we drive our own RPC ping

        try
        {
            await socket.ConnectAsync(endpoint.Url, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            socket.Dispose();
            throw new SurrealConnectionException(
                $"Failed to open WebSocket to {endpoint.Url}: {ex.Message}", ex);
        }

        if (!string.Equals(socket.SubProtocol, CborSubprotocol, StringComparison.OrdinalIgnoreCase))
        {
            socket.Dispose();
            throw new SurrealConnectionException(
                $"Server did not negotiate the 'cbor' sub-protocol (got '{socket.SubProtocol}'). " +
                "Ensure the server supports CBOR over WebSocket.");
        }

        var conn = new WebSocketConnection(socket, endpoint);
        Volatile.Write(ref conn._connected, 1);
        conn._sendLoop = Task.Run(conn.SendLoopAsync);
        conn._receiveLoop = Task.Run(conn.ReceiveLoopAsync);
        conn._pingLoop = Task.Run(conn.PingLoopAsync);
        return conn;
    }

    public async Task<Value> SendAsync(Command command, CancellationToken ct = default)
    {
        var epochAtStart = Volatile.Read(ref _reauthEpoch);
        try
        {
            return await SendOnceAsync(command, ct).ConfigureAwait(false);
        }
        catch (SurrealAuthException ex) when (ex.IsTokenExpired && ReauthHandler is not null
                                              && command is not SigninCommand and not AuthenticateCommand)
        {
            await ReauthAsync(epochAtStart, ct).ConfigureAwait(false);
            return await SendOnceAsync(command, ct).ConfigureAwait(false);
        }
    }

    private async Task<Value> SendOnceAsync(Command command, CancellationToken ct)
    {
        if (!IsConnected)
            throw new SurrealConnectionException("WebSocket is not connected.");

        var id = Interlocked.Increment(ref _nextId);
        var request = RpcRequest.FromCommand(id, command);

        var tcs = new TaskCompletionSource<RpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!_pending.TryAdd(id, tcs))
            throw new InvalidOperationException($"Request id {id} already pending — overflow?");

        try
        {
            await _outbound.Writer.WriteAsync(request, ct).ConfigureAwait(false);

            using var timeout = new CancellationTokenSource(_endpoint.Config.RequestTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token, _shutdown.Token);

            using var registration = linked.Token.Register(static state =>
            {
                var (tcs, token) = ((TaskCompletionSource<RpcResponse>, CancellationToken))state!;
                tcs.TrySetCanceled(token);
            }, (tcs, linked.Token));

            var response = await tcs.Task.ConfigureAwait(false);

            if (response.Error is { } err)
                throw err.ToException();

            return response.Result ?? Value.None;
        }
        finally
        {
            _pending.TryRemove(id, out _);
        }
    }

    private async Task ReauthAsync(long epochAtStart, CancellationToken ct)
    {
        await _reauthGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Coalesce: if another waiter already advanced the epoch, the re-auth happened.
            if (Volatile.Read(ref _reauthEpoch) != epochAtStart) return;
            if (ReauthHandler is { } handler)
                await handler(ct).ConfigureAwait(false);
            Interlocked.Increment(ref _reauthEpoch);
        }
        finally
        {
            _reauthGate.Release();
        }
    }

    private async Task SendLoopAsync()
    {
        try
        {
            await foreach (var request in _outbound.Reader.ReadAllAsync(_shutdown.Token).ConfigureAwait(false))
            {
                var bytes = request.Encode();
                await _sendLock.WaitAsync(_shutdown.Token).ConfigureAwait(false);
                try
                {
                    await _socket.SendAsync(
                        bytes.AsMemory(),
                        WebSocketMessageType.Binary,
                        endOfMessage: true,
                        _shutdown.Token).ConfigureAwait(false);
                }
                finally
                {
                    _sendLock.Release();
                }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            FailAllPending(new SurrealConnectionException("Send loop failed.", ex));
        }
    }

    private async Task ReceiveLoopAsync()
    {
        var buffer = ArrayPool<byte>.Shared.Rent(64 * 1024);
        try
        {
            while (!_shutdown.IsCancellationRequested && _socket.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await _socket.ReceiveAsync(buffer, _shutdown.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await _socket.CloseOutputAsync(
                            WebSocketCloseStatus.NormalClosure, null, CancellationToken.None)
                            .ConfigureAwait(false);
                        FailAllPending(new SurrealConnectionException("Server closed the connection."));
                        return;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Binary)
                    continue; // ignore unexpected text frames

                if (ms.Length > _endpoint.Config.MaxMessageSize)
                {
                    FailAllPending(new SurrealConnectionException(
                        $"Inbound message exceeded MaxMessageSize ({ms.Length} > {_endpoint.Config.MaxMessageSize})."));
                    return;
                }

                DispatchResponse(ms.GetBuffer().AsMemory(0, (int)ms.Length));
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
        catch (Exception ex)
        {
            FailAllPending(new SurrealConnectionException("Receive loop failed.", ex));
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            Volatile.Write(ref _connected, 0);
        }
    }

    private void DispatchResponse(ReadOnlyMemory<byte> payload)
    {
        RpcResponse response;
        try
        {
            response = RpcResponse.Decode(payload);
        }
        catch (Exception ex)
        {
            // We can't route a malformed message, but it shouldn't tear down the loop.
            // Fail the oldest pending request as a best-effort signal.
            var pending = _pending.Values.FirstOrDefault();
            pending?.TrySetException(new SurrealProtocolException("Malformed RPC response.", ex));
            return;
        }

        if (response.Id is not { } id)
        {
            // No id → unsolicited frame; live notifications are the canonical case.
            DispatchLiveNotification(response);
            return;
        }

        if (_pending.TryRemove(id, out var tcs))
            tcs.TrySetResult(response);
    }

    private void DispatchLiveNotification(RpcResponse response)
    {
        if (TryParseNotification(response.Result) is not { } notification) return;
        if (!_liveSubscriptions.TryGetValue(notification.LiveQueryId, out var writer)) return;
        // Channel was created with the consumer-chosen FullMode — TryWrite reflects that
        // policy (DropNewest returns true after dropping; Wait blocks; etc.).
        writer.TryWrite(notification);
    }

    /// <summary>
    /// Parse the SurrealDB live-notification wire shape from the <c>result</c> object:
    /// <c>{ id: uuid, session?: uuid, action: "CREATE"|"UPDATE"|"DELETE", record: any, result: any }</c>.
    /// Returns null if the payload doesn't match the expected shape (silently dropped).
    /// </summary>
    internal static Notification? TryParseNotification(Value? value)
    {
        if (value is not ObjectValue { Object: var obj }) return null;
        if (!obj.TryGetValue("id", out var idValue) || idValue is not UuidValue { Value: var id })
            return null;
        if (!obj.TryGetValue("action", out var actionValue) || actionValue is not StringValue actionStr)
            return null;

        NotificationAction action;
        switch (actionStr.Value.ToUpperInvariant())
        {
            case "CREATE": action = NotificationAction.Create; break;
            case "UPDATE": action = NotificationAction.Update; break;
            case "DELETE": action = NotificationAction.Delete; break;
            default: return null;
        }

        var record = obj.TryGetValue("record", out var r) ? r : Value.None;
        var resultPayload = obj.TryGetValue("result", out var rp) ? rp : Value.None;

        return new Notification(id, action, record, resultPayload);
    }

    private async Task PingLoopAsync()
    {
        var interval = _endpoint.Config.PingInterval;
        if (interval <= TimeSpan.Zero) return;
        try
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(_shutdown.Token).ConfigureAwait(false))
            {
                if (!IsConnected) return;
                try
                {
                    await SendAsync(new HealthCommand(), _shutdown.Token).ConfigureAwait(false);
                }
                catch
                {
                    // ping failures will surface via the receive loop
                }
            }
        }
        catch (OperationCanceledException) { /* normal shutdown */ }
    }

    private void FailAllPending(Exception ex)
    {
        Volatile.Write(ref _connected, 0);
        foreach (var (id, tcs) in _pending.ToArray())
        {
            if (_pending.TryRemove(id, out _))
                tcs.TrySetException(ex);
        }
        // Complete every live subscription with the same exception so consumers'
        // `await foreach` terminates loudly rather than hanging forever.
        foreach (var (id, writer) in _liveSubscriptions.ToArray())
        {
            if (_liveSubscriptions.TryRemove(id, out _))
                writer.TryComplete(ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _connected, 0) == 0 && _sendLoop is null)
            return;

        _outbound.Writer.TryComplete();
        _shutdown.Cancel();

        if (_socket.State == WebSocketState.Open)
        {
            try
            {
                await _socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, null, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch { /* swallow */ }
        }

        var loops = new[] { _sendLoop, _receiveLoop, _pingLoop }
            .Where(t => t is not null)
            .Cast<Task>()
            .ToArray();
        try
        {
            await Task.WhenAll(loops).ConfigureAwait(false);
        }
        catch { /* swallow */ }

        FailAllPending(new SurrealConnectionException("Connection disposed."));

        _socket.Dispose();
        _shutdown.Dispose();
        _sendLock.Dispose();
    }
}
