using System.Globalization;

namespace Disruptor.Surreal.Values;

/// <summary>
/// A datetime with full nanosecond precision.
/// </summary>
/// <remarks>
/// SurrealDB's wire format encodes datetimes as <c>(seconds: i64, nanos: u32)</c> from the
/// Unix epoch. <see cref="DateTimeOffset"/> only resolves to 100ns ticks, so the extra
/// sub-tick nanoseconds (0–99) are preserved separately to round-trip without loss.
/// </remarks>
public readonly struct SurrealDateTime : IEquatable<SurrealDateTime>, IComparable<SurrealDateTime>
{
    /// <summary>Seconds since Unix epoch.</summary>
    public long Seconds { get; }

    /// <summary>Nanoseconds within the second (0 ≤ Nanos &lt; 1_000_000_000).</summary>
    public uint Nanos { get; }

    public SurrealDateTime(long seconds, uint nanos)
    {
        if (nanos >= 1_000_000_000u)
            throw new ArgumentOutOfRangeException(nameof(nanos), "Nanos must be < 1_000_000_000.");
        Seconds = seconds;
        Nanos = nanos;
    }

    public SurrealDateTime(DateTimeOffset value)
    {
        var utc = value.ToUniversalTime();
        var ticks = utc.UtcTicks - DateTimeOffset.UnixEpoch.UtcTicks; // 100ns ticks

        // C# integer division truncates toward zero. For pre-epoch instants like
        // 1969-12-31T23:59:59.500Z (ticks = -5_000_000), naive division yields
        // Seconds=0 and a negative sub-second remainder, which then wraps into an
        // invalid Nanos value when cast to uint. Apply a floor-style adjustment so
        // the remainder is always in [0, TicksPerSecond) and the invariant
        // 0 ≤ Nanos < 1_000_000_000 holds for every reachable DateTimeOffset.
        var seconds = Math.DivRem(ticks, TimeSpan.TicksPerSecond, out var subSecondTicks);
        if (subSecondTicks < 0)
        {
            seconds--;
            subSecondTicks += TimeSpan.TicksPerSecond;
        }

        Seconds = seconds;
        Nanos = (uint)(subSecondTicks * 100);
    }

    /// <summary>The current UTC instant.</summary>
    public static SurrealDateTime UtcNow => new(DateTimeOffset.UtcNow);

    /// <summary>Truncates sub-tick nanoseconds when converting back to <see cref="DateTimeOffset"/>.</summary>
    public DateTimeOffset ToDateTimeOffset()
    {
        var ticks = Seconds * TimeSpan.TicksPerSecond + Nanos / 100;
        return DateTimeOffset.UnixEpoch.AddTicks(ticks);
    }

    public bool Equals(SurrealDateTime other) => Seconds == other.Seconds && Nanos == other.Nanos;
    public override bool Equals(object? obj) => obj is SurrealDateTime d && Equals(d);
    public override int GetHashCode() => HashCode.Combine(Seconds, Nanos);

    public int CompareTo(SurrealDateTime other)
    {
        var c = Seconds.CompareTo(other.Seconds);
        return c != 0 ? c : Nanos.CompareTo(other.Nanos);
    }

    public static bool operator ==(SurrealDateTime a, SurrealDateTime b) => a.Equals(b);
    public static bool operator !=(SurrealDateTime a, SurrealDateTime b) => !a.Equals(b);
    public static bool operator <(SurrealDateTime a, SurrealDateTime b) => a.CompareTo(b) < 0;
    public static bool operator >(SurrealDateTime a, SurrealDateTime b) => a.CompareTo(b) > 0;
    public static bool operator <=(SurrealDateTime a, SurrealDateTime b) => a.CompareTo(b) <= 0;
    public static bool operator >=(SurrealDateTime a, SurrealDateTime b) => a.CompareTo(b) >= 0;

    public static implicit operator SurrealDateTime(DateTimeOffset value) => new(value);
    public static implicit operator DateTimeOffset(SurrealDateTime value) => value.ToDateTimeOffset();

    public override string ToString() =>
        ToDateTimeOffset().ToString("yyyy-MM-ddTHH:mm:ss.fffffffK", CultureInfo.InvariantCulture);
}
