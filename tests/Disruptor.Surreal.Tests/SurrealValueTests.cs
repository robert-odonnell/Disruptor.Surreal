using Disruptor.Surreal.Values;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class SurrealValueTests
{
    [Fact]
    public void None_And_Null_AreSingletons()
    {
        Assert.Same(SurrealNoneValue.Instance, SurrealValue.None);
        Assert.Same(SurrealNullValue.Instance, SurrealValue.Null);
        Assert.NotEqual<SurrealValue>(SurrealValue.None, SurrealValue.Null);
    }

    [Fact]
    public void Equality_IsStructural()
    {
        SurrealValue a = new SurrealRecordId("person", "jaime");
        SurrealValue b = new SurrealRecordId("person", "jaime");
        Assert.Equal(a, b);
        Assert.Equal(a.GetHashCode(), b.GetHashCode());
    }

    [Fact]
    public void ImplicitConversions_Work()
    {
        SurrealValue v1 = 42;
        SurrealValue v2 = "hello";
        SurrealValue v3 = true;
        SurrealValue v4 = 3.14;
        SurrealValue v5 = 99.99m;

        Assert.IsType<SurrealNumberValue>(v1);
        Assert.IsType<StringSurrealValue>(v2);
        Assert.IsType<SurrealBoolValue>(v3);
        Assert.IsType<SurrealNumberValue>(v4);
        Assert.IsType<SurrealNumberValue>(v5);

        Assert.Equal(SurrealNumberKind.Int, ((SurrealNumberValue)v1).SurrealNumber.Kind);
        Assert.Equal(SurrealNumberKind.Float, ((SurrealNumberValue)v4).SurrealNumber.Kind);
        Assert.Equal(SurrealNumberKind.Decimal, ((SurrealNumberValue)v5).SurrealNumber.Kind);
    }

    [Fact]
    public void RecordId_ParseSimple_RoundTrips()
    {
        var id = SurrealRecordId.ParseSimple("person:jaime");
        Assert.Equal("person", id.Table.Name);
        var key = Assert.IsType<SurrealStringRecordIdKey>(id.Key);
        Assert.Equal("jaime", key.Value);
    }

    [Fact]
    public void Datetime_PreservesNanos()
    {
        var d = new SurrealDateTime(seconds: 1_700_000_000, nanos: 123_456_789);
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
