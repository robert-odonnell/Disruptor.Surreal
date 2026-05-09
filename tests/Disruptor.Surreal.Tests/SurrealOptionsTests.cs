using Disruptor.Surreal.Connection;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class SurrealOptionsTests
{
    [Fact]
    public void Parse_BasicShape()
    {
        var opts = SurrealOptions.Parse("Url=ws://localhost:8000;Namespace=test;Database=test;User=root;Password=root");
        Assert.Equal("ws://localhost:8000", opts.Url);
        Assert.Equal("test", opts.Namespace);
        Assert.Equal("test", opts.Database);
        Assert.Equal("root", opts.User);
        Assert.Equal("root", opts.Password);
    }

    [Fact]
    public void Parse_AcceptsAliases()
    {
        var opts = SurrealOptions.Parse("Server=ws://h:8000;NS=ns;DB=db;Uid=u;Pwd=p");
        Assert.Equal("ws://h:8000", opts.Url);
        Assert.Equal("ns", opts.Namespace);
        Assert.Equal("db", opts.Database);
        Assert.Equal("u", opts.User);
        Assert.Equal("p", opts.Password);
    }

    [Fact]
    public void Parse_TrimsWhitespaceAndUnquotes()
    {
        var opts = SurrealOptions.Parse(" Url = \"ws://h:8000\" ; Password = 'sekret' ");
        Assert.Equal("ws://h:8000", opts.Url);
        Assert.Equal("sekret", opts.Password);
    }

    [Fact]
    public void Parse_DoesNotHandleSemicolonsInsideValues()
    {
        // Documented limitation: splits on every ';'. Callers needing literals must
        // construct SurrealOptions directly. Passwords with ';' won't round-trip
        // through this parser.
        Assert.Throws<FormatException>(() =>
            SurrealOptions.Parse("Url=ws://h:8000;Password='p;has;semis'"));
    }

    [Fact]
    public void Parse_AppliesNumericConfig()
    {
        var opts = SurrealOptions.Parse(
            "Url=ws://h:8000;RequestTimeout=10;PingInterval=2;MaxMessageSize=1048576");
        Assert.Equal(TimeSpan.FromSeconds(10), opts.Config.RequestTimeout);
        Assert.Equal(TimeSpan.FromSeconds(2), opts.Config.PingInterval);
        Assert.Equal(1_048_576, opts.Config.MaxMessageSize);
    }

    [Fact]
    public void Parse_RequiresUrl()
    {
        Assert.Throws<FormatException>(() => SurrealOptions.Parse("Namespace=ns;Database=db"));
    }

    [Fact]
    public void Parse_RejectsUnknownKeys()
    {
        Assert.Throws<FormatException>(() => SurrealOptions.Parse("Url=ws://h;Wat=1"));
    }
}
