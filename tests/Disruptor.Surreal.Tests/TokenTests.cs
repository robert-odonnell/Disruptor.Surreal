using Disruptor.Surreal.Auth;
using Disruptor.Surreal.Values;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class TokenTests
{
    [Fact]
    public void AccessToken_RedactsToString()
    {
        var t = new AccessToken("eyJhbGciOiJIUzI1NiJ9.x.y");
        Assert.Equal("AccessToken(REDACTED)", t.ToString());
        Assert.Equal("eyJhbGciOiJIUzI1NiJ9.x.y", t.AsInsecureToken());
    }

    [Fact]
    public void RefreshToken_RedactsToString()
    {
        var t = new RefreshToken("refresh-secret");
        Assert.Equal("RefreshToken(REDACTED)", t.ToString());
        Assert.Equal("refresh-secret", t.AsInsecureToken());
    }

    [Fact]
    public void Token_FromAccessTokenString_HasNoRefresh()
    {
        var t = Token.FromAccessTokenString("abc");
        Assert.Equal("abc", t.Access.AsInsecureToken());
        Assert.Null(t.Refresh);
    }

    [Fact]
    public void Token_FromValue_AcceptsStringForLegacy()
    {
        Value v = "legacy-jwt";
        var t = Token.FromValue(v);
        Assert.Equal("legacy-jwt", t.Access.AsInsecureToken());
        Assert.Null(t.Refresh);
    }

    [Fact]
    public void Token_FromValue_AcceptsObjectWithRefresh()
    {
        Value v = new ObjectValue(new SurrealObject
        {
            ["access"] = "jwt-a",
            ["refresh"] = "jwt-r",
        });
        var t = Token.FromValue(v);
        Assert.Equal("jwt-a", t.Access.AsInsecureToken());
        Assert.NotNull(t.Refresh);
        Assert.Equal("jwt-r", t.Refresh!.AsInsecureToken());
    }

    [Fact]
    public void Token_FromValue_AcceptsObjectWithoutRefresh()
    {
        Value v = new ObjectValue(new SurrealObject { ["access"] = "jwt-a" });
        var t = Token.FromValue(v);
        Assert.Equal("jwt-a", t.Access.AsInsecureToken());
        Assert.Null(t.Refresh);
    }

    [Fact]
    public void Token_ToValue_EncodesAccessOnlyAsString()
    {
        var t = new Token(new AccessToken("jwt-a"));
        var v = t.ToValue();
        Assert.IsType<StringValue>(v);
        Assert.Equal("jwt-a", ((StringValue)v).Value);
    }

    [Fact]
    public void Token_ToValue_EncodesAccessAndRefreshAsObject()
    {
        var t = new Token(new AccessToken("jwt-a"), new RefreshToken("jwt-r"));
        var v = Assert.IsType<ObjectValue>(t.ToValue());
        Assert.Equal("jwt-a", ((StringValue)v.Object["access"]).Value);
        Assert.Equal("jwt-r", ((StringValue)v.Object["refresh"]).Value);
    }

    [Fact]
    public void Token_FromValue_RejectsNonStringNonObject()
    {
        Value v = 42L;
        Assert.Throws<SurrealProtocolException>(() => Token.FromValue(v));
    }
}
