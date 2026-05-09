using Disruptor.Surreal.Values;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class ValueTests
{
    [Fact]
    public void None_And_Null_AreSingletons()
    {
        Assert.Same(NoneValue.Instance, Value.None);
        Assert.Same(NullValue.Instance, Value.Null);
        Assert.NotEqual<Value>(Value.None, Value.Null);
    }

    [Fact]
    public void Equality_IsStructural()
    {
        Value a = new RecordId("person", "jaime");
        Value b = new RecordId("person", "jaime");
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ImplicitConversions_Work()
    {
        Value v1 = 42;
        Value v2 = "hello";
        Value v3 = true;
        Value v4 = 3.14;
        Value v5 = 99.99m;

        Assert.IsType<NumberValue>(v1);
        Assert.IsType<StringValue>(v2);
        Assert.IsType<BoolValue>(v3);
        Assert.IsType<NumberValue>(v4);
        Assert.IsType<NumberValue>(v5);

        Assert.Equal(NumberKind.Int, ((NumberValue)v1).Number.Kind);
        Assert.Equal(NumberKind.Float, ((NumberValue)v4).Number.Kind);
        Assert.Equal(NumberKind.Decimal, ((NumberValue)v5).Number.Kind);
    }

    [Fact]
    public void RecordId_ParseSimple_RoundTrips()
    {
        var id = RecordId.ParseSimple("person:jaime");
        Assert.Equal("person", id.Table.Name);
        var key = Assert.IsType<StringRecordIdKey>(id.Key);
        Assert.Equal("jaime", key.Value);
    }

    [Fact]
    public void Datetime_PreservesNanos()
    {
        var d = new Datetime(seconds: 1_700_000_000, nanos: 123_456_789);
        Assert.Equal(1_700_000_000, d.Seconds);
        Assert.Equal(123_456_789u, d.Nanos);
    }

    [Fact]
    public void Object_PreservesInsertionOrder()
    {
        var obj = new SurrealObject
        {
            ["c"] = 3L,
            ["a"] = 1L,
            ["b"] = 2L,
        };

        Assert.Equal(new[] { "c", "a", "b" }, obj.Keys.ToArray());
    }
}
