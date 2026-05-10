using Disruptor.Surreal.Auth;
using Disruptor.Surreal.Values;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class SurrealTokenTests
{
    [Fact]
    public void AccessToken_RedactsToString()
    {
        var t = new SurrealAccessToken("eyJhbGciOiJIUzI1NiJ9.x.y");
        Assert.Equal("AccessToken(REDACTED)", t.ToString());
        Assert.Equal("eyJhbGciOiJIUzI1NiJ9.x.y", t.AsInsecureToken());
    }

    [Fact]
    public void RefreshToken_RedactsToString()
    {
        var t = new SurrealRefreshToken("refresh-secret");
        Assert.Equal("RefreshToken(REDACTED)", t.ToString());
        Assert.Equal("refresh-secret", t.AsInsecureToken());
    }

    [Fact]
    public void Token_FromAccessTokenString_HasNoRefresh()
    {
        var t = SurrealToken.FromAccessTokenString("abc");
        Assert.Equal("abc", t.SurrealAccess.AsInsecureToken());
        Assert.Null(t.Refresh);
    }

    [Fact]
    public void Token_FromValue_AcceptsStringForLegacy()
    {
        SurrealValue v = "legacy-jwt";
        var t = SurrealToken.FromValue(v);
        Assert.Equal("legacy-jwt", t.SurrealAccess.AsInsecureToken());
        Assert.Null(t.Refresh);
    }

    [Fact]
    public void Token_FromValue_AcceptsObjectWithRefresh()
    {
        SurrealValue v = new SurrealObjectValue(new SurrealObject
        {
            ["access"] = "jwt-a",
            ["refresh"] = "jwt-r",
        });
        var t = SurrealToken.FromValue(v);
        Assert.Equal("jwt-a", t.SurrealAccess.AsInsecureToken());
        Assert.NotNull(t.Refresh);
        Assert.Equal("jwt-r", t.Refresh!.AsInsecureToken());
    }

    [Fact]
    public void Token_FromValue_AcceptsObjectWithoutRefresh()
    {
        SurrealValue v = new SurrealObjectValue(new SurrealObject { ["access"] = "jwt-a" });
        var t = SurrealToken.FromValue(v);
        Assert.Equal("jwt-a", t.SurrealAccess.AsInsecureToken());
        Assert.Null(t.Refresh);
    }

    [Fact]
    public void Token_ToValue_EncodesAccessOnlyAsString()
    {
        var t = new SurrealToken(new SurrealAccessToken("jwt-a"));
        var v = t.ToValue();
        Assert.IsType<StringSurrealValue>(v);
        Assert.Equal("jwt-a", ((StringSurrealValue)v).Value);
    }

    [Fact]
    public void Token_ToValue_EncodesAccessAndRefreshAsObject()
    {
        var t = new SurrealToken(new SurrealAccessToken("jwt-a"), new SurrealRefreshToken("jwt-r"));
        var v = Assert.IsType<SurrealObjectValue>(t.ToValue());
        Assert.Equal("jwt-a", ((StringSurrealValue)v.Object["access"]).Value);
        Assert.Equal("jwt-r", ((StringSurrealValue)v.Object["refresh"]).Value);
    }

    [Fact]
    public void Token_FromValue_RejectsNonStringNonObject()
    {
        SurrealValue v = 42L;
        Assert.Throws<SurrealProtocolException>(() => SurrealToken.FromValue(v));
    }
}
