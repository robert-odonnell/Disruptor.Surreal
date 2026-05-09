using System.Threading.Channels;
using Disruptor.Surreal.Connection;
using Disruptor.Surreal.Values;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class NotificationParseTests
{
    [Theory]
    [InlineData("CREATE", NotificationAction.Create)]
    [InlineData("UPDATE", NotificationAction.Update)]
    [InlineData("DELETE", NotificationAction.Delete)]
    [InlineData("create", NotificationAction.Create)] // case-insensitive
    public void TryParseNotification_HappyPath(string actionStr, NotificationAction expected)
    {
        var liveId = Guid.Parse("019e0c7f-9766-7153-96b4-3d74654cc4aa");
        Value value = new ObjectValue(new SurrealObject
        {
            ["id"] = new UuidValue(liveId),
            ["action"] = actionStr,
            ["record"] = new RecordIdValue(new RecordId("person", "jaime")),
            ["result"] = new ObjectValue(new SurrealObject { ["name"] = "Jaime" }),
        });

        var n = WebSocketConnection.TryParseNotification(value);
        Assert.NotNull(n);
        Assert.Equal(liveId, n.LiveQueryId);
        Assert.Equal(expected, n.Action);
        Assert.IsType<RecordIdValue>(n.Record);
        Assert.IsType<ObjectValue>(n.Result);
    }

    [Fact]
    public void TryParseNotification_RejectsBadShape()
    {
        Assert.Null(WebSocketConnection.TryParseNotification(null));
        Assert.Null(WebSocketConnection.TryParseNotification(Value.None));
        Assert.Null(WebSocketConnection.TryParseNotification(new ObjectValue(new SurrealObject { ["id"] = "not-a-uuid" })));
        Assert.Null(WebSocketConnection.TryParseNotification(new ObjectValue(new SurrealObject
        {
            ["id"] = new UuidValue(Guid.NewGuid()),
            ["action"] = "TWERK",
        })));
    }

    [Fact]
    public void TryParseNotification_DefaultsRecordAndResultToNone()
    {
        var n = WebSocketConnection.TryParseNotification(new ObjectValue(new SurrealObject
        {
            ["id"] = new UuidValue(Guid.NewGuid()),
            ["action"] = "CREATE",
        }));
        Assert.NotNull(n);
        Assert.Same(Value.None, n.Record);
        Assert.Same(Value.None, n.Result);
    }
}

public class DroppedCountingChannelWriterTests
{
    private static (Channel<Notification> ch, DroppedCountingChannelWriter writer, DroppedCounter dropped)
        Build(int capacity, BoundedChannelFullMode policy)
    {
        var ch = Channel.CreateBounded<Notification>(new BoundedChannelOptions(capacity)
        {
            SingleReader = true,
            SingleWriter = true,
            FullMode = BoundedChannelFullMode.Wait,
        });
        var dropped = new DroppedCounter();
        return (ch, new DroppedCountingChannelWriter(ch, dropped, policy), dropped);
    }

    private static Notification N() =>
        new(Guid.NewGuid(), NotificationAction.Create, Value.None, Value.None);

    [Fact]
    public void DropNewest_DropsAndCounts()
    {
        var (_, writer, dropped) = Build(2, BoundedChannelFullMode.DropNewest);
        Assert.True(writer.TryWrite(N()));
        Assert.True(writer.TryWrite(N()));
        Assert.True(writer.TryWrite(N())); // dropped
        Assert.True(writer.TryWrite(N())); // dropped
        Assert.Equal(2, dropped.Value);
    }

    [Fact]
    public async Task DropOldest_DrainsOneAndCounts()
    {
        var (ch, writer, dropped) = Build(2, BoundedChannelFullMode.DropOldest);
        Assert.True(writer.TryWrite(N()));
        Assert.True(writer.TryWrite(N()));
        Assert.True(writer.TryWrite(N())); // drops oldest, writes new
        Assert.Equal(1, dropped.Value);

        // Two notifications still readable (both newer than the dropped one)
        Assert.True(await ch.Reader.WaitToReadAsync());
        Assert.True(ch.Reader.TryRead(out _));
        Assert.True(ch.Reader.TryRead(out _));
    }

    [Fact]
    public void Wait_ReturnsFalseOnFull()
    {
        var (_, writer, dropped) = Build(1, BoundedChannelFullMode.Wait);
        Assert.True(writer.TryWrite(N()));
        Assert.False(writer.TryWrite(N())); // signal "would block"
        Assert.Equal(0, dropped.Value);
    }
}
