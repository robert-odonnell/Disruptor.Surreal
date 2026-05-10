using Disruptor.Surreal.Values;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class PatchTests
{
    [Fact]
    public void Add_BuildsExpectedShape()
    {
        var op = Patch.Add("/name", "Jaime");
        Assert.Equal("add", ((StringSurrealValue)op["op"]).Value);
        Assert.Equal("/name", ((StringSurrealValue)op["path"]).Value);
        Assert.Equal("Jaime", ((StringSurrealValue)op["value"]).Value);
    }

    [Fact]
    public void Replace_BuildsExpectedShape()
    {
        var op = Patch.Replace("/age", 31L);
        Assert.Equal("replace", ((StringSurrealValue)op["op"]).Value);
        Assert.Equal("/age", ((StringSurrealValue)op["path"]).Value);
        Assert.Equal(31L, ((SurrealNumberValue)op["value"]).SurrealNumber.AsInt());
    }

    [Fact]
    public void Remove_OmitsValue()
    {
        var op = Patch.Remove("/admin");
        Assert.Equal("remove", ((StringSurrealValue)op["op"]).Value);
        Assert.Equal("/admin", ((StringSurrealValue)op["path"]).Value);
        Assert.False(op.ContainsKey("value"));
    }

    [Fact]
    public void Move_HasFromAndPath()
    {
        var op = Patch.Move("/old", "/new");
        Assert.Equal("move", ((StringSurrealValue)op["op"]).Value);
        Assert.Equal("/old", ((StringSurrealValue)op["from"]).Value);
        Assert.Equal("/new", ((StringSurrealValue)op["path"]).Value);
    }

    [Fact]
    public void Test_HasOpPathValue()
    {
        var op = Patch.Test("/locked", true);
        Assert.Equal("test", ((StringSurrealValue)op["op"]).Value);
        Assert.True(((SurrealBoolValue)op["value"]).Value);
    }
}
