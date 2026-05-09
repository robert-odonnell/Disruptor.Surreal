using Disruptor.Surreal.Connection;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class ServerVersionTests
{
    [Theory]
    [InlineData("3.0.5", 3, 0, 5, null)]
    [InlineData("surrealdb-3.0.5", 3, 0, 5, null)]
    [InlineData("surrealdb-3.0.0-alpha.1", 3, 0, 0, "alpha.1")]
    [InlineData("surrealdb-3.1.2-rc.4", 3, 1, 2, "rc.4")]
    [InlineData("3.0.0-beta.3", 3, 0, 0, "beta.3")]
    public void Parse_ExtractsComponents(string input, int major, int minor, int patch, string? pre)
    {
        var v = ServerVersion.Parse(input);
        Assert.Equal(major, v.Major);
        Assert.Equal(minor, v.Minor);
        Assert.Equal(patch, v.Patch);
        Assert.Equal(pre, v.PreRelease);
    }

    [Fact]
    public void Parse_RejectsGarbage()
    {
        Assert.Throws<FormatException>(() => ServerVersion.Parse("not-a-version"));
    }

    [Theory]
    [InlineData("3.0.0", true)]
    [InlineData("3.0.5", true)]
    [InlineData("3.9.99", true)]
    [InlineData("3.0.0-alpha.1", true)]
    [InlineData("3.0.0-rc.5", true)]
    [InlineData("2.9.0", false)]
    [InlineData("4.0.0", false)]
    [InlineData("4.0.0-alpha.1", false)]
    [InlineData("0.0.1", false)]
    public void IsSupported_MatchesRustRange(string text, bool expected)
    {
        var v = ServerVersion.Parse(text);
        Assert.Equal(expected, SupportedVersion.IsSupported(v));
    }

    [Fact]
    public void Compare_OrdersCorrectly()
    {
        var a = ServerVersion.Parse("3.0.5");
        var b = ServerVersion.Parse("3.1.0");
        var c = ServerVersion.Parse("3.0.5-rc.1");
        Assert.True(a.CompareTo(b) < 0);
        Assert.True(b.CompareTo(a) > 0);
        Assert.Equal(0, a.CompareTo(c)); // pre-release ignored, matches Rust's strip-pre behaviour
    }
}
