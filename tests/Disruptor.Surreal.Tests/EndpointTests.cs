using Disruptor.Surreal.Connection;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class EndpointTests
{
    [Theory]
    [InlineData("ws://localhost:8000", EndpointKind.Ws, "ws://localhost:8000/rpc")]
    [InlineData("wss://example.com:8080", EndpointKind.Wss, "wss://example.com:8080/rpc")]
    [InlineData("ws://localhost:8000/rpc", EndpointKind.Ws, "ws://localhost:8000/rpc")]
    [InlineData("localhost:8000", EndpointKind.Ws, "ws://localhost:8000/rpc")]
    public void Parse_ExtractsKindAndAppendsRpcPath(string input, EndpointKind kind, string expectedUri)
    {
        var endpoint = Endpoint.Parse(input);
        Assert.Equal(kind, endpoint.Kind);
        Assert.Equal(expectedUri, endpoint.Url.AbsoluteUri.TrimEnd('/'));
    }

    [Fact]
    public void Parse_RejectsUnsupportedScheme()
    {
        var ex = Assert.Throws<ArgumentException>(() => Endpoint.Parse("http://localhost:8000"));
        Assert.Contains("ws/wss", ex.Message);
    }

    [Fact]
    public void Parse_UsesProvidedConfig()
    {
        var config = new ConnectionConfig { RequestTimeout = TimeSpan.FromSeconds(5) };
        var endpoint = Endpoint.Parse("ws://localhost:8000", config);
        Assert.Equal(TimeSpan.FromSeconds(5), endpoint.Config.RequestTimeout);
    }
}
