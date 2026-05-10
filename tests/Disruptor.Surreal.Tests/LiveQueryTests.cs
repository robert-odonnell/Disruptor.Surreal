using System.Threading.Channels;
using Disruptor.Surreal.Connection;
using Disruptor.Surreal.Values;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class NotificationParseTests
{
    [Theory]
    [InlineData("CREATE", SurrealNotificationAction.Create)]
    [InlineData("UPDATE", SurrealNotificationAction.Update)]
    [InlineData("DELETE", SurrealNotificationAction.Delete)]
    [InlineData("create", SurrealNotificationAction.Create)] // case-insensitive
    public void TryParseNotification_HappyPath(string actionStr, SurrealNotificationAction expected)
    {
        var liveId = Guid.Parse("019e0c7f-9766-7153-96b4-3d74654cc4aa");
        SurrealValue surrealValue = new SurrealObjectValue(new SurrealObject
        {
            ["id"] = new SurrealUuidValue(liveId),
            ["action"] = actionStr,
            ["record"] = new SurrealRecordIdValue(new SurrealRecordId("person", "jaime")),
            ["result"] = new SurrealObjectValue(new SurrealObject { ["name"] = "Jaime" }),
        });

        var n = SurrealWebSocketConnection.TryParseNotification(surrealValue);
        Assert.NotNull(n);
        Assert.Equal(liveId, n.LiveQueryId);
        Assert.Equal(expected, n.Action);
        Assert.IsType<SurrealRecordIdValue>(n.Record);
        Assert.IsType<SurrealObjectValue>(n.Result);
    }

    [Fact]
    public void TryParseNotification_RejectsBadShape()
    {
        Assert.Null(SurrealWebSocketConnection.TryParseNotification(null));
        Assert.Null(SurrealWebSocketConnection.TryParseNotification(SurrealValue.None));
        Assert.Null(SurrealWebSocketConnection.TryParseNotification(new SurrealObjectValue(new SurrealObject { ["id"] = "not-a-uuid" })));
        Assert.Null(SurrealWebSocketConnection.TryParseNotification(new SurrealObjectValue(new SurrealObject
        {
            ["id"] = new SurrealUuidValue(Guid.NewGuid()),
            ["action"] = "TWERK",
        })));
    }

    [Fact]
    public void TryParseNotification_DefaultsRecordAndResultToNone()
    {
        var n = SurrealWebSocketConnection.TryParseNotification(new SurrealObjectValue(new SurrealObject
        {
            ["id"] = new SurrealUuidValue(Guid.NewGuid()),
            ["action"] = "CREATE",
        }));
        Assert.NotNull(n);
        Assert.Same(SurrealValue.None, n.Record);
        Assert.Same(SurrealValue.None, n.Result);
    }
}

public class DroppedCountingChannelWriterTests
{
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

    private static SurrealNotification N() =>
        new(Guid.NewGuid(), SurrealNotificationAction.Create, SurrealValue.None, SurrealValue.None);

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
