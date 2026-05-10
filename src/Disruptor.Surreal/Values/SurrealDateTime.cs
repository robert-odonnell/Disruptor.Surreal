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
        Seconds = ticks / TimeSpan.TicksPerSecond;
        var subSecondTicks = ticks - Seconds * TimeSpan.TicksPerSecond;
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
