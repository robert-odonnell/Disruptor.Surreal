using System.Threading.Channels;
using Disruptor.Surreal.Connection;

namespace Disruptor.Surreal;

/// <summary>
/// A live-query subscription. Read notifications via <c>await foreach</c>; dispose to
/// send the matching <c>kill</c> RPC and stop receiving.
/// </summary>
public sealed class LiveQueryHandle : IAsyncEnumerable<Notification>, IAsyncDisposable
{
    private readonly IConnection _connection;
    private readonly ChannelReader<Notification> _reader;
    private readonly DroppedCounter _dropped;
    private int _settled;

    /// <summary>The live-query id assigned by the server.</summary>
    public Guid Id { get; }

    /// <summary>
    /// Number of notifications dropped because the buffer was full and the consumer
    /// fell behind. Only non-zero with <see cref="BoundedChannelFullMode.DropNewest"/>
    /// or <see cref="BoundedChannelFullMode.DropOldest"/>.
    /// </summary>
    public long DroppedCount => _dropped.Value;

    internal LiveQueryHandle(
        IConnection connection,
        Guid id,
        ChannelReader<Notification> reader,
        DroppedCounter dropped)
    {
        _connection = connection;
        Id = id;
        _reader = reader;
        _dropped = dropped;
    }

    /// <inheritdoc />
    public IAsyncEnumerator<Notification> GetAsyncEnumerator(CancellationToken ct = default)
        => _reader.ReadAllAsync(ct).GetAsyncEnumerator(ct);

    /// <summary>
    /// Stop the subscription: deregister locally and send a best-effort <c>kill</c>
    /// RPC. Subsequent enumeration ends as the channel completes.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref _settled, 1) != 0) return;

        _connection.UnregisterLiveSubscription(Id);

        if (_connection.IsConnected)
        {
            try
            {
                await _connection.SendAsync(new KillCommand(Id), CancellationToken.None)
                    .ConfigureAwait(false);
            }
            catch
            {
                // best-effort; the server will GC the live query when the connection drops
            }
        }
    }
}

/// <summary>
/// Mutable counter shared between the connection's dispatch path (which increments on
/// drop) and <see cref="LiveQueryHandle.DroppedCount"/> (which reads). Internal so
/// users only see the read-side.
/// </summary>
internal sealed class DroppedCounter
{
    private long _value;
    public long Value => Interlocked.Read(ref _value);
    public void Increment() => Interlocked.Increment(ref _value);
}

/// <summary>
/// Writer wrapper that drives the user's chosen <see cref="BoundedChannelFullMode"/>
/// policy ourselves over a Wait-mode inner channel, so we can observe and count drops
/// (which the BCL's bounded-channel modes don't expose externally).
/// </summary>
internal sealed class DroppedCountingChannelWriter : ChannelWriter<Notification>
{
    private readonly Channel<Notification> _inner;
    private readonly DroppedCounter _dropped;
    private readonly BoundedChannelFullMode _policy;

    public DroppedCountingChannelWriter(
        Channel<Notification> inner,
        DroppedCounter dropped,
        BoundedChannelFullMode policy)
    {
        _inner = inner;
        _dropped = dropped;
        _policy = policy;
    }

    public override bool TryWrite(Notification item)
    {
        if (_inner.Writer.TryWrite(item)) return true;

        // Inner channel is full.
        switch (_policy)
        {
            case BoundedChannelFullMode.DropNewest:
                _dropped.Increment();
                return true;

            case BoundedChannelFullMode.DropOldest:
                while (_inner.Reader.TryRead(out _))
                {
                    _dropped.Increment();
                    if (_inner.Writer.TryWrite(item)) return true;
                }
                _dropped.Increment();
                return true;

            case BoundedChannelFullMode.DropWrite:
                _dropped.Increment();
                return true;

            case BoundedChannelFullMode.Wait:
            default:
                // Caller asked for back-pressure; report failure so they can
                // fall through to WriteAsync if they want to actually wait.
                return false;
        }
    }

    public override async ValueTask WriteAsync(Notification item, CancellationToken ct = default)
    {
        if (TryWrite(item)) return;
        // Wait policy + full channel: actually wait for space. Note this stalls
        // the connection's receive loop while it's blocked here — the documented
        // foot-gun of choosing Wait on a shared connection.
        await _inner.Writer.WriteAsync(item, ct).ConfigureAwait(false);
    }

    public override bool TryComplete(Exception? error = null) => _inner.Writer.TryComplete(error);

    public override ValueTask<bool> WaitToWriteAsync(CancellationToken ct = default) =>
        _inner.Writer.WaitToWriteAsync(ct);
}
