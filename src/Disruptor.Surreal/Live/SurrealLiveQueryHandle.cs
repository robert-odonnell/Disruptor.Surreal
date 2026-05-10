using System.Threading.Channels;
using Disruptor.Surreal.Connection;

namespace Disruptor.Surreal;

/// <summary>
/// A live-query subscription. Read notifications via <c>await foreach</c>; dispose to
/// send the matching <c>kill</c> RPC and stop receiving.
/// </summary>
public sealed class SurrealLiveQueryHandle : IAsyncEnumerable<SurrealNotification>, IAsyncDisposable
{
    private readonly ISurrealConnection surrealConnection;
    private readonly ChannelReader<SurrealNotification> reader;
    private readonly DroppedCounter dropped;
    private int settled;

    /// <summary>The live-query id assigned by the server.</summary>
    public Guid Id { get; }

    /// <summary>
    /// Number of notifications dropped because the buffer was full and the consumer
    /// fell behind. Only non-zero with <see cref="BoundedChannelFullMode.DropNewest"/>
    /// or <see cref="BoundedChannelFullMode.DropOldest"/>.
    /// </summary>
    public long DroppedCount => dropped.Value;

    internal SurrealLiveQueryHandle(
        ISurrealConnection surrealConnection,
        Guid id,
        ChannelReader<SurrealNotification> reader,
        DroppedCounter dropped)
    {
        this.surrealConnection = surrealConnection;
        Id = id;
        this.reader = reader;
        this.dropped = dropped;
    }

    /// <inheritdoc />
    public IAsyncEnumerator<SurrealNotification> GetAsyncEnumerator(CancellationToken ct = default)
        => reader.ReadAllAsync(ct).GetAsyncEnumerator(ct);

    /// <summary>
    /// Stop the subscription: deregister locally and send a best-effort <c>kill</c>
    /// RPC. Subsequent enumeration ends as the channel completes.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        if (Interlocked.Exchange(ref settled, 1) != 0) return;

        surrealConnection.UnregisterLiveSubscription(Id);

        if (surrealConnection.IsConnected)
        {
            try
            {
                await surrealConnection.SendAsync(new KillCommand(Id), CancellationToken.None)
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
/// drop) and <see cref="SurrealLiveQueryHandle.DroppedCount"/> (which reads). Internal so
/// users only see the read-side.
/// </summary>
internal sealed class DroppedCounter
{
    private long value;
    public long Value => Interlocked.Read(ref value);
    public void Increment() => Interlocked.Increment(ref value);
}

/// <summary>
/// Writer wrapper that drives the user's chosen <see cref="BoundedChannelFullMode"/>
/// policy ourselves over a Wait-mode inner channel, so we can observe and count drops
/// (which the BCL's bounded-channel modes don't expose externally).
/// </summary>
internal sealed class DroppedCountingChannelWriter(
    Channel<SurrealNotification> inner,
    DroppedCounter dropped,
    BoundedChannelFullMode policy) : ChannelWriter<SurrealNotification>
{
    public override bool TryWrite(SurrealNotification item)
    {
        if (inner.Writer.TryWrite(item)) return true;

        // Inner channel is full.
        switch (policy)
        {
            case BoundedChannelFullMode.DropNewest:
                dropped.Increment();
                return true;

            case BoundedChannelFullMode.DropOldest:
                while (inner.Reader.TryRead(out _))
                {
                    dropped.Increment();
                    if (inner.Writer.TryWrite(item)) return true;
                }
                dropped.Increment();
                return true;

            case BoundedChannelFullMode.DropWrite:
                dropped.Increment();
                return true;

            case BoundedChannelFullMode.Wait:
            default:
                // Caller asked for back-pressure; report failure so they can
                // fall through to WriteAsync if they want to actually wait.
                return false;
        }
    }

    public override async ValueTask WriteAsync(SurrealNotification item, CancellationToken ct = default)
    {
        if (TryWrite(item)) return;
        // Wait policy + full channel: actually wait for space. Note this stalls
        // the connection's receive loop while it's blocked here — the documented
        // foot-gun of choosing Wait on a shared connection.
        await inner.Writer.WriteAsync(item, ct).ConfigureAwait(false);
    }

    public override bool TryComplete(Exception? error = null) => inner.Writer.TryComplete(error);

    public override ValueTask<bool> WaitToWriteAsync(CancellationToken ct = default) =>
        inner.Writer.WaitToWriteAsync(ct);
}
