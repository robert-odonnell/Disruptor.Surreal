using System.Threading.Channels;
using Disruptor.Surreal;
using Disruptor.Surreal.Values;
using Xunit;

namespace Disruptor.Surreal.Tests;

/// <summary>
/// Covers the dispatch-path semantics around <see cref="BoundedChannelFullMode"/>
/// that previously fell through the cracks: <c>FullMode.Wait</c> must actually wait
/// (not silently drop), and drops must surface on <see cref="SurrealLiveQueryHandle.DroppedCount"/>.
/// </summary>
public class LiveDispatchTests
{
    private static SurrealNotification N() =>
        new(Guid.NewGuid(), SurrealNotificationAction.Create, SurrealValue.None, SurrealValue.None);

    private static (Channel<SurrealNotification> ch, DroppedCountingChannelWriter writer, DroppedCounter dropped)
        Build(int capacity, BoundedChannelFullMode policy)
    {
        var ch = Channel.CreateBounded<SurrealNotification>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
        var dropped = new DroppedCounter();
        return (ch, new DroppedCountingChannelWriter(ch, dropped, policy), dropped);
    }

    [Fact]
    public async Task WaitMode_WriteAsync_ActuallyBlocksUntilSpace()
    {
        // Capacity 1, Wait policy: first write fills the buffer, second write must wait
        // for the consumer to drain rather than silently dropping.
        var (ch, writer, dropped) = Build(1, BoundedChannelFullMode.Wait);
        Assert.True(writer.TryWrite(N()));            // synchronous fast path
        Assert.False(writer.TryWrite(N()));           // signals "would block"

        var second = N();
        var pending = writer.WriteAsync(second).AsTask();
        Assert.False(pending.IsCompleted);            // genuinely waiting

        Assert.True(ch.Reader.TryRead(out _));        // consumer drains one
        await pending.WaitAsync(TimeSpan.FromSeconds(1));

        Assert.True(ch.Reader.TryRead(out var read));
        Assert.Equal(second.LiveQueryId, read.LiveQueryId);
        Assert.Equal(0L, dropped.Value);              // Wait never drops
    }

    [Fact]
    public async Task WaitMode_WriteAsync_HonorsCancellation()
    {
        var (_, writer, _) = Build(1, BoundedChannelFullMode.Wait);
        Assert.True(writer.TryWrite(N()));

        using var cts = new CancellationTokenSource();
        var pending = writer.WriteAsync(N(), cts.Token).AsTask();
        Assert.False(pending.IsCompleted);
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => pending);
    }

    [Fact]
    public void DropWrite_AndDropNewest_BehaveIdenticallyInThisWrapper()
    {
        // Documents the intentional equivalence: both bump DroppedCount, both drop
        // the new arrival when full.
        var (_, w1, d1) = Build(1, BoundedChannelFullMode.DropNewest);
        var (_, w2, d2) = Build(1, BoundedChannelFullMode.DropWrite);

        Assert.True(w1.TryWrite(N()));
        Assert.True(w1.TryWrite(N())); // dropped
        Assert.True(w2.TryWrite(N()));
        Assert.True(w2.TryWrite(N())); // dropped

        Assert.Equal(1L, d1.Value);
        Assert.Equal(1L, d2.Value);
    }
}
