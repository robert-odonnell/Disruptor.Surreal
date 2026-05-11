using Disruptor.Surreal.Values;
using Xunit;

namespace Disruptor.Surreal.Tests;

public class SurrealDateTimeTests
{
    [Fact]
    public void Epoch_IsZero()
    {
        var d = new SurrealDateTime(DateTimeOffset.UnixEpoch);
        Assert.Equal(0L, d.Seconds);
        Assert.Equal(0u, d.Nanos);
    }

    [Fact]
    public void PostEpoch_FractionalSecond_PreservesNanos()
    {
        // 1970-01-01T00:00:00.500Z → +0.5 seconds from epoch
        var dto = new DateTimeOffset(1970, 1, 1, 0, 0, 0, TimeSpan.Zero).AddMilliseconds(500);
        var d = new SurrealDateTime(dto);
        Assert.Equal(0L, d.Seconds);
        Assert.Equal(500_000_000u, d.Nanos);
    }

    [Fact]
    public void PreEpoch_FractionalSecond_FloorsCorrectly()
    {
        // 1969-12-31T23:59:59.500Z → -0.5 seconds from epoch.
        // Truncate-toward-zero division was producing seconds=0 + a negative remainder
        // that wrapped into ~3.79B as uint — well outside the [0, 1B) invariant.
        var dto = new DateTimeOffset(1969, 12, 31, 23, 59, 59, TimeSpan.Zero).AddMilliseconds(500);
        var d = new SurrealDateTime(dto);
        Assert.Equal(-1L, d.Seconds);
        Assert.Equal(500_000_000u, d.Nanos);
    }

    [Fact]
    public void PreEpoch_OneNanosecondShortOfEpoch_FloorsToMaxNanos()
    {
        // 1969-12-31T23:59:59.9999999Z → -100ns. Edge case: max representable Nanos
        // in DateTimeOffset precision (100ns ticks → 999_999_900 ns).
        var dto = new DateTimeOffset(1969, 12, 31, 23, 59, 59, TimeSpan.Zero).AddTicks(9_999_999);
        var d = new SurrealDateTime(dto);
        Assert.Equal(-1L, d.Seconds);
        Assert.Equal(999_999_900u, d.Nanos);
    }

    [Fact]
    public void PreEpoch_WholeSecond_NanosIsZero()
    {
        var dto = new DateTimeOffset(1969, 12, 31, 23, 59, 59, TimeSpan.Zero);
        var d = new SurrealDateTime(dto);
        Assert.Equal(-1L, d.Seconds);
        Assert.Equal(0u, d.Nanos);
    }

    [Fact]
    public void Invariant_Holds_AcrossWideRange()
    {
        // Sweep a few interesting instants and confirm Nanos < 1B every time.
        var inputs = new[]
        {
            DateTimeOffset.UnixEpoch.AddTicks(-1),
            DateTimeOffset.UnixEpoch.AddTicks(-10_000_000),
            DateTimeOffset.UnixEpoch.AddYears(-50),
            DateTimeOffset.UnixEpoch.AddSeconds(-3600.123456),
            DateTimeOffset.UnixEpoch.AddYears(50).AddMilliseconds(789),
        };
        foreach (var dto in inputs)
        {
            var d = new SurrealDateTime(dto);
            Assert.InRange(d.Nanos, 0u, 999_999_999u);
        }
    }

    [Fact]
    public void RoundTrip_PreEpoch_PreservesInstant()
    {
        var dto = new DateTimeOffset(1969, 12, 31, 23, 59, 59, TimeSpan.Zero).AddMilliseconds(500);
        var d = new SurrealDateTime(dto);
        var roundtripped = d.ToDateTimeOffset();
        Assert.Equal(dto, roundtripped);
    }
}
