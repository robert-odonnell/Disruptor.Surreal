using System.Threading.Channels;
using Disruptor.Surreal;
using Disruptor.Surreal.Connection;
using Disruptor.Surreal.Values;
using Xunit;

namespace Disruptor.Surreal.Tests;

/// <summary>
/// Demonstrates that a consumer can fake the wire layer for unit tests by implementing
/// the public <see cref="IConnection"/> interface and constructing a <see cref="Surreal"/>
/// directly. This is the seam Disruptor.Surface (and any other ORM-style consumer) uses.
/// </summary>
public class FakeConnectionTests
{
    private sealed class RecordingConnection : IConnection
    {
        public List<(string Method, Value? Params, Guid? TxnId)> Sent { get; } = [];
        public Func<string, Value?, Guid?, Value>? Responder { get; set; }

        public bool IsConnected { get; set; } = true;
        public Func<CancellationToken, Task>? ReauthHandler { get; set; }

        public Task<Value> SendAsync(string method, Value? @params, Guid? txnId, CancellationToken ct = default)
        {
            Sent.Add((method, @params, txnId));
            var v = Responder?.Invoke(method, @params, txnId) ?? Value.None;
            return Task.FromResult(v);
        }

        public void RegisterLiveSubscription(Guid liveQueryId, ChannelWriter<Notification> writer) { }
        public void UnregisterLiveSubscription(Guid liveQueryId) { }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Surreal_RoutesQueryThroughIConnection()
    {
        var fake = new RecordingConnection
        {
            Responder = (method, @params, txn) =>
                // Server returns the query() shape: array of statement-result objects.
                new ArrayValue(
                    [
                        new ObjectValue(
                            new SurrealObject
                            {
                                ["status"] = "OK",
                                ["time"] = "1ms",
                                ["result"] = new ObjectValue(
                                    new SurrealObject
                                    {
                                        ["answer"] = 42L
                                    }
                                ),
                            }
                        )
                    ]
                ),
        };

        await using var db = new Surreal(fake);
        var response = await db.QueryAsync("SELECT 42 AS answer");

        var (s, _, txnId) = Assert.Single(fake.Sent);
        Assert.Equal("query", s);
        Assert.Null(txnId);
        var result = response.Take(0);
        var obj = Assert.IsType<ObjectValue>(result);
        Assert.Equal(42L, ((NumberValue)obj.Object["answer"]).Number.AsInt());
    }

    [Fact]
    public async Task Surreal_PropagatesRpcErrorsThroughIConnection()
    {
        var fake = new RecordingConnection
        {
            Responder = (_, _, _) => throw new SurrealConflictException(0, "transaction conflict"),
        };

        await using var db = new Surreal(fake);
        await Assert.ThrowsAsync<SurrealConflictException>(() => db.QueryAsync("SELECT 1"));
    }

    [Fact]
    public async Task Surreal_SendsExpectedWireShapeForUseAsync()
    {
        var fake = new RecordingConnection();
        await using var db = new Surreal(fake);
        await db.UseAsync("ns", "db");

        var (method, args, _) = Assert.Single(fake.Sent);
        Assert.Equal("use", method);
        var arr = Assert.IsType<ArrayValue>(args);    
        Assert.Equal(2, arr.Array.Count);
        Assert.Equal("ns", ((StringValue)arr.Array[0]).Value);
        Assert.Equal("db", ((StringValue)arr.Array[1]).Value);
    }

    [Fact]
    public async Task Surreal_ThreadsTxnIdInsideTransaction()
    {
        var liveTxn = Guid.NewGuid();
        var fake = new RecordingConnection
        {
            Responder = (method, _, _) => method == "begin"
                ? new UuidValue(liveTxn)
                : Value.None,
        };

        await using var db = new Surreal(fake);
        await using var tx = await db.BeginTransactionAsync();
        await tx.QueryAsync("SELECT 1");

        Assert.Equal("begin", fake.Sent[0].Method);
        Assert.Null(fake.Sent[0].TxnId);

        Assert.Equal("query", fake.Sent[1].Method);
        Assert.Equal(liveTxn, fake.Sent[1].TxnId);
    }
}
