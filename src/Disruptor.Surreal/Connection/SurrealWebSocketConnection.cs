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
internal sealed class SurrealWebSocketConnection : ISurrealConnection
{
    private const string CborSubprotocol = "cbor";

    private readonly ClientWebSocket socket;
    private readonly SurrealEndpoint surrealEndpoint;
    private readonly Channel<RpcRequest> outbound;
    private readonly ConcurrentDictionary<long, TaskCompletionSource<RpcResponse>> pending = new();
    private readonly ConcurrentDictionary<Guid, ChannelWriter<SurrealNotification>> liveSubscriptions = new();
    private readonly CancellationTokenSource shutdown = new();
    private readonly SemaphoreSlim sendLock = new(1, 1);

    private long nextId;
    private Task? sendLoop;
    private Task? receiveLoop;
    private Task? pingLoop;
    private int connected;
    private readonly SemaphoreSlim reauthGate = new(1, 1);
    private long reauthEpoch;

    /// <summary>
    /// Caller-installed re-auth callback. When a request fails with a token-expired
    /// <see cref="SurrealAuthException"/>, the connection invokes this to refresh
    /// authentication then retries the original request once. Set by the client
    /// after a successful signin.
    /// </summary>
    public Func<CancellationToken, Task>? ReauthHandler { get; set; }

    public void RegisterLiveSubscription(Guid liveQueryId, ChannelWriter<SurrealNotification> writer)
    {
        if (!liveSubscriptions.TryAdd(liveQueryId, writer))
            throw new InvalidOperationException(
                $"Live subscription for {liveQueryId} is already registered.");
    }

    public void UnregisterLiveSubscription(Guid liveQueryId)
    {
        if (liveSubscriptions.TryRemove(liveQueryId, out var writer))
            writer.TryComplete();
    }

    private SurrealWebSocketConnection(ClientWebSocket socket, SurrealEndpoint surrealEndpoint)
    {
        this.socket = socket;
        this.surrealEndpoint = surrealEndpoint;
        outbound = Channel.CreateBounded<RpcRequest>(
            new BoundedChannelOptions(capacity: 1024)
            {
                SingleReader = true,
                FullMode = BoundedChannelFullMode.Wait,
            });
    }

    public bool IsConnected => Volatile.Read(ref connected) == 1
        && socket.State == WebSocketState.Open;

    public static async Task<SurrealWebSocketConnection> ConnectAsync(SurrealEndpoint surrealEndpoint, CancellationToken ct)
    {
        var socket = new ClientWebSocket();
        socket.Options.AddSubProtocol(CborSubprotocol);
        socket.Options.KeepAliveInterval = TimeSpan.Zero; // we drive our own RPC ping

        try
        {
            await socket.ConnectAsync(surrealEndpoint.Url, ct).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            socket.Dispose();
            throw new SurrealConnectionException(
                $"Failed to open WebSocket to {surrealEndpoint.Url}: {ex.Message}", ex);
        }

        if (!string.Equals(socket.SubProtocol, CborSubprotocol, StringComparison.OrdinalIgnoreCase))
        {
            socket.Dispose();
            throw new SurrealConnectionException(
                $"Server did not negotiate the 'cbor' sub-protocol (got '{socket.SubProtocol}'). " +
                "Ensure the server supports CBOR over WebSocket.");
        }

        var conn = new SurrealWebSocketConnection(socket, surrealEndpoint);
        Volatile.Write(ref conn.connected, 1);
        conn.sendLoop = Task.Run(conn.SendLoopAsync, ct);
        conn.receiveLoop = Task.Run(conn.ReceiveLoopAsync, ct);
        conn.pingLoop = Task.Run(conn.PingLoopAsync, ct);
        return conn;
    }

    public async Task<SurrealValue> SendAsync(string method, SurrealValue? @params, Guid? txnId, CancellationToken ct = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(method);
        var epochAtStart = Volatile.Read(ref reauthEpoch);
        try
        {
            return await SendOnceAsync(method, @params, txnId, ct).ConfigureAwait(false);
        }
        catch (SurrealAuthException ex) when (ex.IsTokenExpired && ReauthHandler is not null
                                              // Don't loop into a re-auth attempt for an auth call itself.
                                              && method is not "signin" and not "authenticate" and not "signup")
        {
            await ReauthAsync(epochAtStart, ct).ConfigureAwait(false);
            return await SendOnceAsync(method, @params, txnId, ct).ConfigureAwait(false);
        }
    }

    private async Task<SurrealValue> SendOnceAsync(string method, SurrealValue? @params, Guid? txnId, CancellationToken ct)
    {
        if (!IsConnected)
            throw new SurrealConnectionException("WebSocket is not connected.");

        var id = Interlocked.Increment(ref nextId);
        var request = new RpcRequest(id, method, @params, txnId);

        var tcs = new TaskCompletionSource<RpcResponse>(TaskCreationOptions.RunContinuationsAsynchronously);
        if (!pending.TryAdd(id, tcs))
            throw new InvalidOperationException($"Request id {id} already pending — overflow?");

        try
        {
            await outbound.Writer.WriteAsync(request, ct).ConfigureAwait(false);

            using var timeout = new CancellationTokenSource(surrealEndpoint.Config.RequestTimeout);
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(ct, timeout.Token, shutdown.Token);

            await using var registration = linked.Token.Register(static state =>
            {
                var (tcs, token) = ((TaskCompletionSource<RpcResponse>, CancellationToken))state!;
                tcs.TrySetCanceled(token);
            }, (tcs, linked.Token));

            var response = await tcs.Task.ConfigureAwait(false);

            if (response.Error is { } err)
                throw err.ToException();

            return response.Result ?? SurrealValue.None;
        }
        finally
        {
            pending.TryRemove(id, out _);
        }
    }

    private async Task ReauthAsync(long epochAtStart, CancellationToken ct)
    {
        await reauthGate.WaitAsync(ct).ConfigureAwait(false);
        try
        {
            // Coalesce: if another waiter already advanced the epoch, the re-auth happened.
            if (Volatile.Read(ref reauthEpoch) != epochAtStart) return;
            if (ReauthHandler is { } handler)
                await handler(ct).ConfigureAwait(false);
            Interlocked.Increment(ref reauthEpoch);
        }
        finally
        {
            reauthGate.Release();
        }
    }

    private async Task SendLoopAsync()
    {
        try
        {
            await foreach (var request in outbound.Reader.ReadAllAsync(shutdown.Token).ConfigureAwait(false))
            {
                var bytes = request.Encode();
                await sendLock.WaitAsync(shutdown.Token).ConfigureAwait(false);
                try
                {
                    await socket.SendAsync(
                        bytes.AsMemory(),
                        WebSocketMessageType.Binary,
                        endOfMessage: true,
                        shutdown.Token).ConfigureAwait(false);
                }
                finally
                {
                    sendLock.Release();
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
            while (!shutdown.IsCancellationRequested && socket.State == WebSocketState.Open)
            {
                using var ms = new MemoryStream();
                WebSocketReceiveResult result;
                do
                {
                    result = await socket.ReceiveAsync(buffer, shutdown.Token).ConfigureAwait(false);
                    if (result.MessageType == WebSocketMessageType.Close)
                    {
                        await socket.CloseOutputAsync(
                            WebSocketCloseStatus.NormalClosure, null, CancellationToken.None)
                            .ConfigureAwait(false);
                        FailAllPending(new SurrealConnectionException("Server closed the connection."));
                        return;
                    }
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType != WebSocketMessageType.Binary)
                    continue; // ignore unexpected text frames

                if (ms.Length > surrealEndpoint.Config.MaxMessageSize)
                {
                    FailAllPending(new SurrealConnectionException(
                        $"Inbound message exceeded MaxMessageSize ({ms.Length} > {surrealEndpoint.Config.MaxMessageSize})."));
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
            Volatile.Write(ref connected, 0);
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
            var firstPending = pending.Values.FirstOrDefault();
            firstPending?.TrySetException(new SurrealProtocolException("Malformed RPC response.", ex));
            return;
        }

        if (response.Id is not { } id)
        {
            // No id → unsolicited frame; live notifications are the canonical case.
            DispatchLiveNotification(response);
            return;
        }

        if (pending.TryRemove(id, out var tcs))
            tcs.TrySetResult(response);
    }

    private void DispatchLiveNotification(RpcResponse response)
    {
        if (TryParseNotification(response.Result) is not { } notification) return;
        if (!liveSubscriptions.TryGetValue(notification.LiveQueryId, out var writer)) return;
        // Channel was created with the consumer-chosen FullMode — TryWrite reflects that
        // policy (DropNewest returns true after dropping; Wait blocks; etc.).
        writer.TryWrite(notification);
    }

    /// <summary>
    /// Parse the SurrealDB live-notification wire shape from the <c>result</c> object:
    /// <c>{ id: uuid, session?: uuid, action: "CREATE"|"UPDATE"|"DELETE", record: any, result: any }</c>.
    /// Returns null if the payload doesn't match the expected shape (silently dropped).
    /// </summary>
    internal static SurrealNotification? TryParseNotification(SurrealValue? value)
    {
        if (value is not SurrealObjectValue { Object: var obj }) return null;
        if (!obj.TryGetValue("id", out var idValue) || idValue is not SurrealUuidValue { Value: var id })
            return null;
        if (!obj.TryGetValue("action", out var actionValue) || actionValue is not StringSurrealValue actionStr)
            return null;

        SurrealNotificationAction action;
        switch (actionStr.Value.ToUpperInvariant())
        {
            case "CREATE": action = SurrealNotificationAction.Create; break;
            case "UPDATE": action = SurrealNotificationAction.Update; break;
            case "DELETE": action = SurrealNotificationAction.Delete; break;
            default: return null;
        }

        var record = obj.TryGetValue("record", out var r) ? r : SurrealValue.None;
        var resultPayload = obj.TryGetValue("result", out var rp) ? rp : SurrealValue.None;

        return new SurrealNotification(id, action, record, resultPayload);
    }

    private async Task PingLoopAsync()
    {
        var interval = surrealEndpoint.Config.PingInterval;
        if (interval <= TimeSpan.Zero) return;
        try
        {
            using var timer = new PeriodicTimer(interval);
            while (await timer.WaitForNextTickAsync(shutdown.Token).ConfigureAwait(false))
            {
                if (!IsConnected) return;
                try
                {
                    await this.SendAsync(new HealthCommand(), shutdown.Token).ConfigureAwait(false);
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
        Volatile.Write(ref connected, 0);
        foreach (var (id, tcs) in pending.ToArray())
        {
            if (pending.TryRemove(id, out _))
                tcs.TrySetException(ex);
        }
        // Complete every live subscription with the same exception so consumers'
        // `await foreach` terminates loudly rather than hanging forever.
        foreach (var (id, writer) in liveSubscriptions.ToArray())
        {
            if (liveSubscriptions.TryRemove(id, out _))
                writer.TryComplete(ex);
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref connected, 0) == 0 && sendLoop is null)
            return;

        outbound.Writer.TryComplete();
        await shutdown.CancelAsync();

        if (socket.State == WebSocketState.Open)
        {
            try
            {
                await socket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, null, CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch { /* swallow */ }
        }

        var loops = new[] { sendLoop, receiveLoop, pingLoop }
            .Where(t => t is not null)
            .Cast<Task>()
            .ToArray();
        try
        {
            await Task.WhenAll(loops).ConfigureAwait(false);
        }
        catch { /* swallow */ }

        FailAllPending(new SurrealConnectionException("Connection disposed."));

        socket.Dispose();
        shutdown.Dispose();
        sendLock.Dispose();
    }
}
