using Disruptor.Surreal;
using Disruptor.Surreal.Values;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class PatchTests
{
    [Fact]
    public void Add_BuildsExpectedShape()
    {
        var op = Patch.Add("/name", "Jaime");
        Assert.Equal("add", ((StringValue)op["op"]).Value);
        Assert.Equal("/name", ((StringValue)op["path"]).Value);
        Assert.Equal("Jaime", ((StringValue)op["value"]).Value);
    }

    [Fact]
    public void Replace_BuildsExpectedShape()
    {
        var op = Patch.Replace("/age", 31L);
        Assert.Equal("replace", ((StringValue)op["op"]).Value);
        Assert.Equal("/age", ((StringValue)op["path"]).Value);
        Assert.Equal(31L, ((NumberValue)op["value"]).Number.AsInt());
    }

    [Fact]
    public void Remove_OmitsValue()
    {
        var op = Patch.Remove("/admin");
        Assert.Equal("remove", ((StringValue)op["op"]).Value);
        Assert.Equal("/admin", ((StringValue)op["path"]).Value);
        Assert.False(op.ContainsKey("value"));
    }

    [Fact]
    public void Move_HasFromAndPath()
    {
        var op = Patch.Move("/old", "/new");
        Assert.Equal("move", ((StringValue)op["op"]).Value);
        Assert.Equal("/old", ((StringValue)op["from"]).Value);
        Assert.Equal("/new", ((StringValue)op["path"]).Value);
    }

    [Fact]
    public void Test_HasOpPathValue()
    {
        var op = Patch.Test("/locked", true);
        Assert.Equal("test", ((StringValue)op["op"]).Value);
        Assert.True(((BoolValue)op["value"]).Value);
    }
}
